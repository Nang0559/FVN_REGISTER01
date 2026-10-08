using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

public sealed class HrmSyncService : IHrmSyncService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object StateLock = new();
    private static HrmSyncRuntimeStatusDto _status = new();

    private readonly IHrmStagingImporterResolver _importers;
    private readonly IHrmSyncJobResolver _jobs;
    private readonly ILogger<HrmSyncService> _logger;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _configuration;

    public HrmSyncService(
        IHrmStagingImporterResolver importers,
        IHrmSyncJobResolver jobs,
        ILogger<HrmSyncService> logger,
        IUnitOfWork uow,
        IConfiguration configuration)
    {
        _importers = importers;
        _jobs = jobs;
        _logger = logger;
        _uow = uow;
        _configuration = configuration;
    }

    // ------------------------------------------------------------------
    // Security provisioning (User / Approver)
    // ------------------------------------------------------------------

    public async Task<ServiceResult<HrmSyncRunResultDto>> ReconcileSecurityAsync(
        string? triggeredBy = null,
        CancellationToken ct = default)
    {
        if (!await Gate.WaitAsync(0, ct))
            return ServiceResult<HrmSyncRunResultDto>.Fail("Đang có một phiên đồng bộ HRM khác chạy. Vui lòng chờ phiên hiện tại hoàn tất.");

        HrmSyncRunResultDto? previous;
        lock (StateLock) previous = _status.LastRun == null ? null : CloneRun(_status.LastRun);

        var run = new HrmSyncRunResultDto
        {
            RunId = Guid.NewGuid(),
            Manual = true,
            Running = true,
            StartedAt = DateTime.Now,
            TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "SYSTEM" : triggeredBy
        };
        SetRunning(run);

        try
        {
            run.Jobs.AddRange(await RunSecurityProvisioningAsync(ct));

            run.Success = run.Jobs.All(x => x.Success);
            run.Running = false;
            run.FinishedAt = DateTime.Now;
            run.Summary = string.Join(" | ", run.Jobs.Select(j => j.Summary));

            SetFinished(MergeForStatus(run, previous));
            return ServiceResult<HrmSyncRunResultDto>.Ok(run);
        }
        catch (OperationCanceledException)
        {
            run.Running = false;
            run.Success = false;
            run.FinishedAt = DateTime.Now;
            run.Summary = "Phiên đồng bộ đã bị hủy.";
            SetFinished(MergeForStatus(run, previous));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HRM-SYNC] Security provisioning failed.");
            run.Success = false;
            run.Running = false;
            run.FinishedAt = DateTime.Now;
            run.Summary = $"Provision User/Approver thất bại: {ex.Message}";
            run.Jobs.Add(new HrmSyncJobRunDto
            {
                EntityType = "Approver",
                SyncOrder = 99,
                Success = false,
                Summary = run.Summary,
                Errors = new List<string> { ex.Message }
            });
            SetFinished(MergeForStatus(run, previous));
            return ServiceResult<HrmSyncRunResultDto>.Ok(run);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static HrmSyncRunResultDto MergeForStatus(HrmSyncRunResultDto security, HrmSyncRunResultDto? previous)
    {
        if (previous == null) return security;

        var merged = CloneRun(security);
        var jobs = previous.Jobs
            .Where(j => !SecurityEntityTypes.Contains(j.EntityType))
            .Concat(security.Jobs)
            .ToList();

        merged.Jobs.Clear();
        merged.Jobs.AddRange(jobs);
        merged.Success = merged.Jobs.All(x => x.Success);
        return merged;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public HrmSyncRuntimeStatusDto GetRuntimeStatus()
    {
        lock (StateLock) return CloneStatus(_status);
    }

    public Task<ServiceResult<HrmSyncRunResultDto>> RunAllAsync(string? triggeredBy = null, bool manual = true, CancellationToken ct = default)
        => ExecuteAsync(null, triggeredBy, manual, ct);

    public Task<ServiceResult<HrmSyncRunResultDto>> RunEntityAsync(string entityType, string? triggeredBy = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            return Task.FromResult(ServiceResult<HrmSyncRunResultDto>.Fail("EntityType không được để trống."));

        var type = entityType.Trim();
        if (SecurityEntityTypes.Contains(type))
            return ReconcileSecurityAsync(triggeredBy, ct);

        return ExecuteAsync(type, triggeredBy, true, ct);
    }

    // ------------------------------------------------------------------
    // Pipeline chính
    // ------------------------------------------------------------------

    private async Task<ServiceResult<HrmSyncRunResultDto>> ExecuteAsync(string? entityType, string? triggeredBy, bool manual, CancellationToken ct)
    {
        if (!await Gate.WaitAsync(0, ct))
            return ServiceResult<HrmSyncRunResultDto>.Fail("Đang có một phiên đồng bộ HRM khác chạy. Vui lòng chờ phiên hiện tại hoàn tất.");

        var run = new HrmSyncRunResultDto
        {
            RunId = Guid.NewGuid(),
            Manual = manual,
            Running = true,
            StartedAt = DateTime.Now,
            TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "SYSTEM" : triggeredBy
        };
        SetRunning(run);

        try
        {
            if (entityType == null)
            {
                var shiftSync = await _uow.SqlQueryRawAsync<ShiftSyncSummary>(
                    "EXEC dbo.usp_SyncHrmShiftMaster;", ct);
                var summary = shiftSync.FirstOrDefault();
                if (summary != null)
                    _logger.LogInformation("[HRM-SYNC] Shift master synced: shifts={Shifts}, schedules={Schedules}, days={Days}, employeeSchedules={EmployeeSchedules}.",
                        summary.ShiftCount, summary.ScheduleCount, summary.ScheduleDayCount, summary.EmployeeScheduleCount);
            }

            if (entityType == null)
            {
                foreach (var importer in _importers.GetAll())
                {
                    ct.ThrowIfCancellationRequested();
                    var count = await importer.ImportAsync(run.StartedAt.Date, ct);
                    _logger.LogInformation("[HRM-SYNC] Imported {Count} rows for {EntityType}.", count, importer.EntityType);
                }

                foreach (var job in _jobs.GetAll().OrderBy(x => x.SyncOrder).ThenBy(x => x.EntityType, StringComparer.OrdinalIgnoreCase))
                {
                    ct.ThrowIfCancellationRequested();
                    var result = await job.RunAsync(ct);
                    AddJobResult(run, job, result);
                    SetProgress(run);
                    if (!result.Success && job.IsBlockingDependency)
                    {
                        run.Success = false;
                        run.Summary = $"Dừng tại job blocking '{job.EntityType}': {result.Summary}";
                        break;
                    }
                }
            }
            else
            {
                var importer = _importers.Resolve(entityType);
                var job = _jobs.Resolve(entityType);
                ct.ThrowIfCancellationRequested();
                var count = await importer.ImportAsync(run.StartedAt.Date, ct);
                _logger.LogInformation("[HRM-SYNC] Imported {Count} rows for {EntityType}.", count, entityType);
                var result = await job.RunAsync(ct);
                AddJobResult(run, job, result);
                SetProgress(run);
            }

            // Security provisioning is expensive because it reconciles all User/Approver
            // rows. Keep it out of the frequent automatic polling loop. Manual RunAll and
            // explicit Security reconciliation still execute it. Production can opt back
            // in with HrmSync:SecurityProvisioningOnAutomatic=true.
            //
            // Self-heal: the main HRM batch is committed before the User/Approver hook runs,
            // so a failed hook would otherwise leave gaps until someone clicks "reconcile".
            // Automatic runs therefore run one cheap COUNT query and only reconcile when
            // there are missing users/approvers (see NeedsSecuritySelfHealAsync).
            var provisionSecurity = entityType == null &&
                (manual
                 || _configuration.GetValue<bool>("HrmSync:SecurityProvisioningOnAutomatic")
                 || await NeedsSecuritySelfHealAsync(ct));

            if (provisionSecurity)
            {
                try
                {
                    run.Jobs.AddRange(await RunSecurityProvisioningAsync(ct));
                    SetProgress(run);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HRM-SYNC] Security provisioning (RunAll) failed.");
                    run.Jobs.Add(new HrmSyncJobRunDto
                    {
                        EntityType = "Approver",
                        SyncOrder = 99,
                        Success = false,
                        Summary = $"Provision thất bại: {ex.Message}",
                        Errors = new List<string> { ex.Message }
                    });
                }
            }

            run.Success = run.Jobs.All(x => x.Success);
            run.Running = false;
            run.FinishedAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(run.Summary))
            {
                var ok = run.Jobs.Count(x => x.Success);
                run.Summary = $"Hoàn tất {run.Jobs.Count} job: {ok} thành công, {run.Jobs.Count - ok} lỗi.";
            }
            SetFinished(run);
            return ServiceResult<HrmSyncRunResultDto>.Ok(run);
        }
        catch (OperationCanceledException)
        {
            run.Running = false; run.Success = false; run.FinishedAt = DateTime.Now; run.Summary = "Phiên đồng bộ đã bị hủy.";
            SetFinished(run);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HRM-SYNC] Run failed. EntityType={EntityType}", entityType);
            run.Running = false; run.Success = false; run.FinishedAt = DateTime.Now; run.Summary = $"Đồng bộ thất bại: {ex.Message}";
            SetFinished(run);
            return ServiceResult<HrmSyncRunResultDto>.Ok(run);
        }
        finally { Gate.Release(); }
    }

    private static readonly HashSet<string> SecurityEntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "User", "Approver", "SecurityProvisioning" };

    private const string ApproverFrom = """
        FROM dbo.F03Employees e
        INNER JOIN dbo.F03ApprovalPolicies ap
            ON ap.ApprovalPositionCode = e.PositionCode AND ap.IsActive = 1
        WHERE e.IsActive = 1 AND ISNULL(e.LevelApprove,0) > 0
        """;

    // DepartmentCode is canonical int/int?. Do not trim/cast it in SQL.
    private const string ApproverMissing = """
        AND NOT EXISTS (
            SELECT 1 FROM dbo.F03Approvers a
            WHERE a.IsActive = 1
              AND a.ApproverCode = e.EmployeeCode
              AND a.RequestType = CASE ap.RequestType
                    WHEN 0 THEN N'Leave' WHEN 1 THEN N'Overtime'
                    WHEN 2 THEN N'Trip'  WHEN 3 THEN N'Equipment' END
              AND a.Level = ap.Level
              AND a.ApproveForDeptCode = ap.DeptCode)
        """;

    // Cheap check used by automatic runs: only the two "missing" counters.
    private const string MissingSecurityStatsSql =
        "SELECT " +
        "(SELECT COUNT(*) FROM dbo.F03Employees e LEFT JOIN dbo.F03Users u ON u.EmployeeCode=e.EmployeeCode " +
        "  WHERE e.IsActive=1 AND u.Id IS NULL) AS MissingUsers, " +
        "(SELECT COUNT(*) " + ApproverFrom + " " + ApproverMissing + ") AS MissingApprovers;";

    private sealed class MissingSecurityStats
    {
        public int MissingUsers { get; set; }
        public int MissingApprovers { get; set; }
    }

    private static readonly object SelfHealLock = new();
    private static DateTime _lastSelfHealUtc = DateTime.MinValue;
    private static int _missingAfterLastSelfHeal;
    private static readonly TimeSpan SelfHealRetryAfter = TimeSpan.FromHours(6);

    /// <summary>
    /// True when automatic polling should run the full security reconcile: there are missing
    /// users/approvers AND (the gap grew since the last reconcile OR the last attempt is old).
    /// The second condition prevents a permanently-unfixable gap from re-running the heavy
    /// procedure every polling cycle.
    /// </summary>
    private async Task<bool> NeedsSecuritySelfHealAsync(CancellationToken ct)
    {
        try
        {
            var row = (await _uow.SqlQueryRawAsync<MissingSecurityStats>(MissingSecurityStatsSql, ct)).FirstOrDefault();
            var missing = (row?.MissingUsers ?? 0) + (row?.MissingApprovers ?? 0);
            if (missing <= 0) return false;

            lock (SelfHealLock)
                return missing > _missingAfterLastSelfHeal
                       || DateTime.UtcNow - _lastSelfHealUtc >= SelfHealRetryAfter;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HRM-SYNC] Security self-heal check failed; skipping automatic reconcile this cycle.");
            return false;
        }
    }

    private const string SecurityStatsSql =
        "SELECT " +
        "(SELECT COUNT(*) FROM dbo.F03Employees WHERE IsActive=1) AS ActiveEmployees, " +
        "(SELECT COUNT(*) FROM dbo.F03Employees WHERE IsActive=1 AND ISNULL(LevelApprove,0)>0) AS ApproverCandidates, " +
        "(SELECT COUNT(*) FROM dbo.F03ApprovalPolicies WHERE IsActive=1) AS ActivePolicies, " +
        "(SELECT COUNT(*) FROM dbo.F03Approvers WHERE IsActive=1) AS ActiveApprovers, " +
        "(SELECT COUNT(*) " + ApproverFrom + ") AS ExpectedApprovers, " +
        "(SELECT COUNT(*) " + ApproverFrom + " " + ApproverMissing + ") AS MissingApprovers, " +
        "(SELECT COUNT(*) FROM dbo.F03Users) AS TotalUsers, " +
        "(SELECT COUNT(*) FROM dbo.F03Employees e LEFT JOIN dbo.F03Users u ON u.EmployeeCode=e.EmployeeCode " +
        "  WHERE e.IsActive=1 AND u.Id IS NULL) AS MissingUsers;";

    private sealed class SecurityStats
    {
        public int ActiveEmployees { get; set; }
        public int ApproverCandidates { get; set; }
        public int ActivePolicies { get; set; }
        public int ActiveApprovers { get; set; }
        public int ExpectedApprovers { get; set; }
        public int MissingApprovers { get; set; }
        public int TotalUsers { get; set; }
        public int MissingUsers { get; set; }
    }

    private sealed class ShiftSyncSummary
    {
        public int ShiftCount { get; set; }
        public int ScheduleCount { get; set; }
        public int ScheduleDayCount { get; set; }
        public int EmployeeScheduleCount { get; set; }
    }

    private async Task<List<HrmSyncJobRunDto>> RunSecurityProvisioningAsync(CancellationToken ct)
    {
        var before = (await _uow.SqlQueryRawAsync<SecurityStats>(SecurityStatsSql, ct)).FirstOrDefault()
                     ?? new SecurityStats();

        await _uow.ExecuteSqlRawAsync(
            """
            EXEC dbo.usp_ReconcileHrmSecurity
                @EmployeeCode=NULL,
                @CreatedBy={0};
            """,
            ct,
            0);

        var after = (await _uow.SqlQueryRawAsync<SecurityStats>(SecurityStatsSql, ct)).FirstOrDefault()
                    ?? new SecurityStats();

        lock (SelfHealLock)
        {
            _lastSelfHealUtc = DateTime.UtcNow;
            _missingAfterLastSelfHeal = after.MissingUsers + after.MissingApprovers;
        }

        var usersFixed = Math.Max(0, before.MissingUsers - after.MissingUsers);
        var approversFixed = Math.Max(0, before.MissingApprovers - after.MissingApprovers);

        var userJob = new HrmSyncJobRunDto
        {
            EntityType = "User",
            SyncOrder = 98,
            IsBlockingDependency = false,
            TotalSource = after.ActiveEmployees,
            Added = usersFixed,
            Updated = 0,
            Deactivated = 0,
            Unchanged = Math.Max(0, before.ActiveEmployees - before.MissingUsers),
            Success = after.MissingUsers == 0,
            Summary = $"User: {after.TotalUsers} tài khoản / {after.ActiveEmployees} nhân viên active; " +
                      $"tạo mới {usersFixed}; còn thiếu {after.MissingUsers}.",
            Errors = new List<string>()
        };
        if (after.MissingUsers > 0)
            userJob.Errors.Add($"Sau provision vẫn còn {after.MissingUsers} nhân viên active chưa có F03Users.");

        string approverSummary =
            after.ApproverCandidates == 0
                ? $"Không có nhân viên nào LevelApprove > 0 (đang có {after.ActiveEmployees} nhân viên active). Kiểm tra job Employee có map LevelApprove từ HRM chưa."
            : after.ActivePolicies == 0
                ? "F03ApprovalPolicies chưa có policy nào đang active."
            : after.ExpectedApprovers == 0
                ? $"Có {after.ApproverCandidates} nhân viên LevelApprove > 0 nhưng không khớp ApprovalPositionCode của {after.ActivePolicies} policy (so PositionCode)."
            : $"Approver: kỳ vọng {after.ExpectedApprovers} phân quyền (từ {after.ApproverCandidates} ứng viên); đang active {after.ActiveApprovers}; tạo mới {approversFixed}; còn thiếu {after.MissingApprovers}.";

        var approverJob = new HrmSyncJobRunDto
        {
            EntityType = "Approver",
            SyncOrder = 99,
            IsBlockingDependency = false,
            TotalSource = after.ExpectedApprovers,
            Added = approversFixed,
            Updated = 0,
            Deactivated = 0,
            Unchanged = Math.Max(0, before.ExpectedApprovers - before.MissingApprovers),
            Success = after.MissingApprovers == 0,
            Summary = approverSummary,
            Errors = new List<string>()
        };
        if (after.MissingApprovers > 0)
            approverJob.Errors.Add($"Sau provision vẫn còn {after.MissingApprovers} phân quyền approver chưa được tạo. Kiểm tra usp_ReconcileHrmSecurity (điều kiện DeptCode/Level/RequestType).");

        if (after.ExpectedApprovers == 0)
            _logger.LogWarning("[HRM-SYNC] Approver provisioning: {Summary}", approverSummary);

        return new List<HrmSyncJobRunDto> { userJob, approverJob };
    }

    private static void AddJobResult(HrmSyncRunResultDto run, IHrmSyncJob job, HrmSyncResult result)
        => run.Jobs.Add(new HrmSyncJobRunDto
        {
            EntityType = job.EntityType,
            SyncOrder = job.SyncOrder,
            IsBlockingDependency = job.IsBlockingDependency,
            Success = result.Success,
            TotalSource = result.TotalSource,
            Added = result.Added,
            Updated = result.Updated,
            Deactivated = result.Deactivated,
            Unchanged = result.Unchanged,
            Superseded = result.Superseded,
            Summary = result.Summary,
            Errors = result.Errors.ToList()
        });

    private static void SetRunning(HrmSyncRunResultDto run)
    {
        lock (StateLock)
        {
            _status = new HrmSyncRuntimeStatusDto
            {
                IsRunning = true,
                CurrentRunId = run.RunId,
                StartedAt = run.StartedAt,
                TriggeredBy = run.TriggeredBy,
                LastRun = null,
                CurrentRun = CloneRun(run)
            };
        }
    }

    // Refresh the live snapshot after each job so the admin UI can show progress mid-run.
    private static void SetProgress(HrmSyncRunResultDto run)
    {
        lock (StateLock)
        {
            if (!_status.IsRunning || _status.CurrentRunId != run.RunId) return;
            _status.CurrentRun = CloneRun(run);
        }
    }

    private static void SetFinished(HrmSyncRunResultDto run)
    {
        lock (StateLock)
        {
            _status = new HrmSyncRuntimeStatusDto
            {
                IsRunning = false,
                CurrentRunId = null,
                StartedAt = run.StartedAt,
                TriggeredBy = run.TriggeredBy,
                LastRun = CloneRun(run),
                CurrentRun = null
            };
        }
    }

    private static HrmSyncRunResultDto CloneRun(HrmSyncRunResultDto source)
        => new()
        {
            RunId = source.RunId,
            Manual = source.Manual,
            Running = source.Running,
            Success = source.Success,
            StartedAt = source.StartedAt,
            FinishedAt = source.FinishedAt,
            TriggeredBy = source.TriggeredBy,
            Summary = source.Summary,
            Jobs = source.Jobs.Select(x => new HrmSyncJobRunDto
            {
                EntityType = x.EntityType,
                SyncOrder = x.SyncOrder,
                IsBlockingDependency = x.IsBlockingDependency,
                Success = x.Success,
                TotalSource = x.TotalSource,
                Added = x.Added,
                Updated = x.Updated,
                Deactivated = x.Deactivated,
                Unchanged = x.Unchanged,
                Superseded = x.Superseded,
                Summary = x.Summary,
                Errors = x.Errors.ToList()
            }).ToList()
        };

    private static HrmSyncRuntimeStatusDto CloneStatus(HrmSyncRuntimeStatusDto source)
        => new()
        {
            IsRunning = source.IsRunning,
            CurrentRunId = source.CurrentRunId,
            StartedAt = source.StartedAt,
            TriggeredBy = source.TriggeredBy,
            LastRun = source.LastRun == null ? null : CloneRun(source.LastRun),
            CurrentRun = source.CurrentRun == null ? null : CloneRun(source.CurrentRun)
        };
}
