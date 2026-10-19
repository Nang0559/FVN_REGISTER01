using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Infrastructure.Models.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

/// <summary>
/// Company-wide attendance calculation scheduler.
///
/// Contract:
/// 1. On application start, catch up the missing company-wide range through yesterday.
/// 2. Every day at 00:30, calculate yesterday for the whole company.
/// 3. Manual department/date calculation uses the same IHrmAttendanceCalculationService
///    and the same dbo.usp_CalculateHrmAttendance procedure.
/// </summary>
public sealed class HrmAttendanceCalculationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HrmAttendanceCalculationWorker> _logger;
    private readonly BackgroundWorkerHealthRegistry _health;
    private readonly IConfiguration _configuration;

    private static readonly TimeSpan DailyRunAt = new(0, 30, 0);

    // Session-scoped SQL applock shared by every API instance: only one company-wide
    // calculation (catch-up or daily) may be alive at a time.
    private const string CompanyRunGuardResource = "FVN_REGISTER:ATTENDANCE:COMPANY-RUN";

    public HrmAttendanceCalculationWorker(
        IServiceProvider serviceProvider,
        ILogger<HrmAttendanceCalculationWorker> logger,
        BackgroundWorkerHealthRegistry health,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _health = health;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[HRM_ATTENDANCE_WORKER] Khởi động.");
        _health.Started(nameof(HrmAttendanceCalculationWorker));

        // Recover abandoned calculation audit rows before any new calculation starts.
        // Exact session_id + login_time + connection_id matching prevents SQL SPID reuse from keeping
        // an old Run alive or incorrectly failing a calculation owned by another API instance.
        await ReconcileAbandonedRunsAsync(stoppingToken);

        // Never wait for the first scheduled run when the application starts.
        // This closes the gap after deployment/restart and guarantees the current
        // payroll period has calculated data before users open the dashboard.
        // Dev/test: đặt HrmAttendanceWorker:CatchUpOnStartup = false để KHÔNG tính bù khi khởi động
        // (mỗi lần restart API sẽ chặn đọc F03HrmAttendanceCalculated trong lúc tính). Mặc định true.
        if (_configuration.GetValue("HrmAttendanceWorker:CatchUpOnStartup", true))
        {
            await RunCatchUpAsync(stoppingToken);
        }
        else
        {
            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Bỏ qua catch-up khi khởi động (HrmAttendanceWorker:CatchUpOnStartup = false).");
        }

        // Refresh today's attendance in one shared background process. Dashboard requests
        // never call HRM/calculation directly, so N approvers still cause only one scheduled
        // company-wide refresh per interval. SQL applock prevents overlap across API instances.
        var refreshMinutes = Math.Clamp(
            _configuration.GetValue<int?>("HrmAttendanceWorker:TodayRefreshMinutes") ?? 15,
            5,
            120);
        var lastDailyRunDate = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                if (now.TimeOfDay >= DailyRunAt && lastDailyRunDate.Date != now.Date)
                {
                    await RunYesterdayAsync(stoppingToken);
                    lastDailyRunDate = now.Date;
                }

                // Reconcile missing company-wide coverage periodically as well as at startup.
                // Failed/abandoned catch-up jobs are re-enqueued from the first uncovered open date.
                await RunCatchUpAsync(stoppingToken);
                await RunTodayAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(refreshMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _health.Failure(nameof(HrmAttendanceCalculationWorker), ex);
                _logger.LogError(ex, "[HRM_ATTENDANCE_WORKER] Lỗi vòng làm mới chấm công hôm nay.");
                await Task.Delay(TimeSpan.FromMinutes(refreshMinutes), stoppingToken);
            }
        }

        _logger.LogInformation("[HRM_ATTENDANCE_WORKER] Dừng.");
    }

    private async Task RunCatchUpAsync(CancellationToken ct)
    {
        try
        {
            await using var guard = await TryAcquireCompanyRunGuardAsync(ct);
            if (guard is null)
            {
                _logger.LogInformation(
                    "[HRM_ATTENDANCE_WORKER] Bỏ qua catch-up: một lần tính company-wide khác đang chạy.");
                return;
            }

            var yesterday = DateTime.Today.AddDays(-1).Date;
            var from = await GetCompanyWideCatchUpStartAsync(yesterday, ct);
            if (!from.HasValue || from.Value > yesterday)
                return;

            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Catch-up company-wide missing range: {From} -> {To} (từng ngày)",
                from.Value.ToString("dd/MM/yyyy"),
                yesterday.ToString("dd/MM/yyyy"));

            await CalculateCompanyWideByDayAsync(from.Value, yesterday, "HRM-ATTENDANCE-WORKER-CATCHUP", ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[HRM_ATTENDANCE_WORKER] Catch-up bị hủy.");
        }
        catch (Exception ex)
        {
            _health.Failure(nameof(HrmAttendanceCalculationWorker), ex);
            _logger.LogError(ex, "[HRM_ATTENDANCE_WORKER] Catch-up lỗi.");
        }
    }

    private async Task RunTodayAsync(CancellationToken ct)
    {
        try
        {
            await using var guard = await TryAcquireCompanyRunGuardAsync(ct);
            if (guard is null)
            {
                _logger.LogDebug(
                    "[HRM_ATTENDANCE_WORKER] Bỏ qua refresh hôm nay: một lần tính company-wide khác đang chạy.");
                return;
            }

            var today = DateTime.Today;
            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Refresh attendance hôm nay: {Date}",
                today.ToString("dd/MM/yyyy"));

            await CalculateCompanyWideAsync(
                today, today, "HRM-ATTENDANCE-WORKER-TODAY", ct, reopenPayrollPeriod: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
        catch (Exception ex)
        {
            _health.Failure(nameof(HrmAttendanceCalculationWorker), ex);
            _logger.LogError(ex, "[HRM_ATTENDANCE_WORKER] Refresh hôm nay thất bại.");
        }
    }

    private async Task RunYesterdayAsync(CancellationToken ct)
    {
        try
        {
            await using var guard = await TryAcquireCompanyRunGuardAsync(ct);
            if (guard is null)
            {
                _logger.LogInformation(
                    "[HRM_ATTENDANCE_WORKER] Bỏ qua lần chạy hằng ngày: một lần tính company-wide khác đang chạy.");
                return;
            }

            var yesterday = DateTime.Today.AddDays(-1).Date;

            var payrollStart = await GetPayrollPeriodStartAsync(yesterday, ct);
            if (!payrollStart.HasValue) return;
            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Daily company-wide calculation: {Date}",
                yesterday.ToString("dd/MM/yyyy"));
            await CalculateCompanyWideByDayAsync(
                yesterday < payrollStart.Value ? payrollStart.Value : yesterday,
                yesterday,
                "HRM-ATTENDANCE-WORKER",
                ct,
                skipCoveredDates: false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[HRM_ATTENDANCE_WORKER] Lần chạy hằng ngày bị hủy.");
        }
        catch (Exception ex)
        {
            _health.Failure(nameof(HrmAttendanceCalculationWorker), ex);
            _logger.LogError(ex, "[HRM_ATTENDANCE_WORKER] Lần chạy hằng ngày lỗi.");
        }
    }

    /// <summary>
    /// One Run row per day. Days are processed in ascending order and processing stops at the first
    /// failure, so the set of Succeeded company-wide runs always stays contiguous and
    /// GetCompanyWideCatchUpStartAsync (MAX(ToDate)+1) resumes exactly where the last run stopped.
    /// </summary>
    private async Task CalculateCompanyWideByDayAsync(
        DateTime from,
        DateTime to,
        string triggeredBy,
        CancellationToken ct,
        bool skipCoveredDates = true)
    {
        if (from.Date > to.Date)
            return;

        // Persist each bounded range before returning. The dedicated job worker owns SQL execution,
        // per-day progress, cancellation and recovery; this scheduler never holds a long SQL call.
        for (var cursor = from.Date; cursor <= to.Date;)
        {
            ct.ThrowIfCancellationRequested();
            var chunkTo = cursor.AddDays(92) < to.Date ? cursor.AddDays(92) : to.Date;
            var queued = await EnqueueCompanyWideAsync(
                cursor,
                chunkTo,
                triggeredBy,
                reopenPayrollPeriod: true,
                skipCoveredDates: skipCoveredDates,
                ct: ct);

            if (!queued)
            {
                _logger.LogWarning(
                    "[HRM_ATTENDANCE_WORKER] Không thể đưa range {From} -> {To} vào job queue; lần catch-up sau sẽ kiểm tra lại.",
                    cursor.ToString("dd/MM/yyyy"),
                    chunkTo.ToString("dd/MM/yyyy"));
                return;
            }

            cursor = chunkTo.AddDays(1);
        }
    }

    private async Task<bool> CalculateCompanyWideAsync(
        DateTime from,
        DateTime to,
        string triggeredBy,
        CancellationToken ct,
        bool reopenPayrollPeriod = true)
    {
        return await EnqueueCompanyWideAsync(
            from.Date,
            to.Date,
            triggeredBy,
            reopenPayrollPeriod,
            skipCoveredDates: false,
            ct: ct);
    }

    private async Task<bool> EnqueueCompanyWideAsync(
        DateTime from,
        DateTime to,
        string triggeredBy,
        bool reopenPayrollPeriod,
        bool skipCoveredDates,
        CancellationToken ct)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IHrmAttendanceCalculationJobService>();
            var result = await jobs.EnqueueAsync(
                new FVN_REGISTER.Contract.Dtos.HrmSync.HrmAttendanceCalculationJobCommandDto
                {
                    // NULL = all active HRM employees/company-wide.
                    DeptCode = null,
                    EmployeeCode = null,
                    FromDate = from.Date,
                    ToDate = to.Date,
                    ReopenPayrollPeriod = reopenPayrollPeriod
                },
                triggeredBy,
                ct,
                skipCoveredDates,
                priority: 2);

            if (result.IsSuccess && result.Data is not null)
            {
                _health.Success(nameof(HrmAttendanceCalculationWorker));
                _logger.LogInformation(
                    "[HRM_ATTENDANCE_WORKER] Job {JobId} đã được đưa vào hàng đợi: {From} -> {To}, SkipCoveredDates={SkipCoveredDates}",
                    result.Data.JobId,
                    from.ToString("dd/MM/yyyy"),
                    to.ToString("dd/MM/yyyy"),
                    skipCoveredDates);
                return true;
            }

            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Bỏ qua/enqueue chưa thành công {From} -> {To}: {Message}",
                from.ToString("dd/MM/yyyy"),
                to.ToString("dd/MM/yyyy"),
                result.Message ?? "Unknown error");
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "[HRM_ATTENDANCE_WORKER] Enqueue job bị hủy {From} -> {To}.",
                from.ToString("dd/MM/yyyy"),
                to.ToString("dd/MM/yyyy"));
            return false;
        }
        catch (Exception ex)
        {
            _health.Failure(nameof(HrmAttendanceCalculationWorker), ex);
            _logger.LogError(
                ex,
                "[HRM_ATTENDANCE_WORKER] Lỗi khi enqueue job {From} -> {To}.",
                from.ToString("dd/MM/yyyy"),
                to.ToString("dd/MM/yyyy"));
            return false;
        }
    }

    /// <summary>
    /// Reconciles stale Running rows for ALL calculation sources (worker, manual and calendar backfill).
    /// A tracked Run is failed only when its exact SQL session (SPID + login_time) no longer exists.
    /// Legacy rows created before session tracking are failed only when no attendance calculation
    /// request and no attendance application lock exists anywhere on this SQL Server.
    /// If DMV permissions are missing, the method fails closed and leaves every Run untouched.
    /// </summary>
    private async Task ReconcileAbandonedRunsAsync(CancellationToken ct)
    {
        var staleMinutes = Math.Clamp(
            _configuration.GetValue<int?>("BackgroundWorkers:StaleMinutes") ?? 15,
            1,
            1440);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();

        try
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @HasActiveAttendanceCalculation bit = 0;

                IF EXISTS
                (
                    SELECT 1
                    FROM sys.dm_exec_requests AS r
                    CROSS APPLY sys.dm_exec_input_buffer(r.session_id,r.request_id) AS ib
                    WHERE r.session_id <> @@SPID
                      AND ib.event_info LIKE N'%usp_CalculateHrmAttendance%'
                )
                OR EXISTS
                (
                    SELECT 1
                    FROM sys.dm_tran_locks AS l
                    WHERE l.resource_type=N'APPLICATION'
                      AND l.resource_description LIKE N'%FVN_REGISTER:ATTENDANCE:DATE:%'
                )
                    SET @HasActiveAttendanceCalculation = 1;

                UPDATE run
                SET Status=N'Failed',
                    FinishedAt=GETDATE(),
                    ErrorMessage=N'Abandoned: SQL session của lần tính đã kết thúc trước khi Run hoàn tất.'
                FROM dbo.F03HrmAttendanceCalculationRun AS run
                WHERE run.Status=N'Running'
                  AND
                  (
                      (
                          run.SqlSessionId IS NOT NULL
                          AND run.SqlSessionLoginTime IS NOT NULL
                          AND run.SqlConnectionId IS NOT NULL
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM sys.dm_exec_sessions AS sessionInfo
                              INNER JOIN sys.dm_exec_connections AS connectionInfo
                                ON connectionInfo.session_id=sessionInfo.session_id
                              WHERE sessionInfo.session_id=run.SqlSessionId
                                AND CONVERT(datetime2(3),sessionInfo.login_time)=run.SqlSessionLoginTime
                                AND connectionInfo.connection_id=run.SqlConnectionId
                          )
                      )
                      OR
                      (
                          (run.SqlSessionId IS NULL OR run.SqlSessionLoginTime IS NULL OR run.SqlConnectionId IS NULL)
                          AND run.StartedAt < DATEADD(minute,{-staleMinutes},GETDATE())
                          AND @HasActiveAttendanceCalculation=0
                      )
                  );
                """, ct);

            if (affected > 0)
                _logger.LogWarning(
                    "[HRM_ATTENDANCE_WORKER] Đã phục hồi {Count} Run 'Running' bị bỏ dở sau khi xác nhận session SQL không còn tồn tại (ngưỡng legacy {Minutes} phút).",
                    affected, staleMinutes);

            var unresolvedLegacy = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM dbo.F03HrmAttendanceCalculationRun
                WHERE Status=N'Running'
                  AND (SqlSessionId IS NULL OR SqlSessionLoginTime IS NULL OR SqlConnectionId IS NULL)
                """).SingleAsync(ct);

            if (unresolvedLegacy > 0)
                _logger.LogWarning(
                    "[HRM_ATTENDANCE_WORKER] Còn {Count} Run chưa có đủ SQL session metadata; không tự động đổi trạng thái vì SQL vẫn có thể đang giữ khóa attendance.",
                    unresolvedLegacy);
        }
        catch (Exception ex)
        {
            // DMV access requires VIEW SERVER STATE (or the SQL Server-version equivalent).
            // Fail closed: never mark rows Failed when the session state cannot be verified.
            _logger.LogError(
                ex,
                "[HRM_ATTENDANCE_WORKER] Không thể đối chiếu SQL sessions; giữ nguyên tất cả Run 'Running'. Kiểm tra quyền VIEW SERVER STATE của tài khoản kết nối FVN.");
        }
    }

    /// <summary>
    /// Takes a session-scoped SQL applock on a dedicated, non-pooled connection that stays open for the
    /// whole company-wide run. Returns null when another run (this or another API instance) holds it.
    /// If this process dies, SQL Server drops the session and the lock goes with it.
    /// </summary>
    private async Task<CompanyRunGuard?> TryAcquireCompanyRunGuardAsync(CancellationToken ct)
    {
        string? connectionString;
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
            connectionString = db.Database.GetConnectionString();
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var builder = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DECLARE @r int;
                EXEC @r = sp_getapplock @Resource=@Resource, @LockMode=N'Exclusive',
                          @LockOwner=N'Session', @LockTimeout=0;
                SELECT @r;
                """;
            cmd.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = CompanyRunGuardResource });
            var rc = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            if (rc < 0)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new CompanyRunGuard(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class CompanyRunGuard : IAsyncDisposable
    {
        private readonly SqlConnection _connection;

        public CompanyRunGuard(SqlConnection connection) => _connection = connection;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await using var cmd = _connection.CreateCommand();
                    cmd.CommandText = "EXEC sp_releaseapplock @Resource=@Resource, @LockOwner=N'Session';";
                    cmd.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = CompanyRunGuardResource });
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                // Closing the (non-pooled) connection below drops the session and its applock anyway.
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }

    private async Task<DateTime?> GetCompanyWideCatchUpStartAsync(DateTime yesterday, CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();

        // Today's refresh is intentionally independent and must not move the catch-up cursor
        // past an uncovered day in the current payroll period.
        var periodStart = await GetPayrollPeriodStartAsync(yesterday, ct);
        if (!periodStart.HasValue || periodStart.Value.Date > yesterday.Date)
            return null;

        var from = DateOnly.FromDateTime(periodStart.Value.Date);
        var to = DateOnly.FromDateTime(yesterday.Date);
        var ranges = await db.Database.SqlQuery<CoverageRun>($"""
            SELECT r.FromDate, r.ToDate
            FROM dbo.F03HrmAttendanceCalculationRun AS r
            WHERE r.Status=N'Succeeded'
              AND r.DeptCode IS NULL
              AND r.EmployeeCode IS NULL
              AND r.FromDate<={to}
              AND r.ToDate>={from}
            ORDER BY r.FromDate
            """).ToListAsync(ct);

        var covered = new HashSet<DateOnly>();
        foreach (var range in ranges)
        {
            var first = range.FromDate < from ? from : range.FromDate;
            var last = range.ToDate > to ? to : range.ToDate;
            for (var date = first; date <= last; date = date.AddDays(1))
                covered.Add(date);
        }

        var candidate = from;
        while (candidate <= to)
        {
            if (covered.Contains(candidate))
            {
                candidate = candidate.AddDays(1);
                continue;
            }

            var period = await db.PayrollCalculationPeriods.AsNoTracking()
                .Where(x => x.IsActive != false
                    && x.FromDate <= candidate
                    && x.ToDate >= candidate)
                .OrderByDescending(x => x.Id)
                .Select(x => new { x.ToDate, x.Status })
                .FirstOrDefaultAsync(ct);

            if (period is not null && (period.Status == "Locked" || period.Status == "Exported"))
            {
                candidate = period.ToDate.AddDays(1);
                continue;
            }

            return candidate.ToDateTime(TimeOnly.MinValue);
        }

        return null;
    }

    private sealed record CoverageRun(DateOnly FromDate, DateOnly ToDate);

    private async Task<DateTime?> GetPayrollPeriodStartAsync(DateTime date, CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
        var period = await db.PayrollCalculationPeriods.AsNoTracking()
            .Where(x => x.IsActive != false && x.FromDate <= DateOnly.FromDateTime(date) && x.ToDate >= DateOnly.FromDateTime(date))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (period is not null)
        {
            if (period.Status is "Locked" or "Exported")
            {
                _logger.LogInformation(
                    "[HRM_ATTENDANCE_WORKER] Bỏ qua ngày {Date}: kỳ {PeriodCode} đã {Status}.",
                    date.ToString("dd/MM/yyyy"), period.PeriodCode, period.Status);
                return null;
            }

            return period.FromDate.ToDateTime(TimeOnly.MinValue);
        }

        // Bootstrap only when no period exists yet; the canonical period is 21 -> 20.
        return date.Day >= 21
            ? new DateTime(date.Year, date.Month, 21)
            : new DateTime(date.Year, date.Month, 1).AddMonths(-1).Date.AddDays(20);
    }
}