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

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = DateTime.Today.Add(DailyRunAt);
            if (now >= nextRun)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;

            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Chờ đến {NextRun} ({Hours:F1}h nữa)",
                nextRun.ToString("dd/MM/yyyy HH:mm"),
                delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunYesterdayAsync(stoppingToken);
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

            await AbandonStaleRunsAsync(ct);

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

            await AbandonStaleRunsAsync(ct);

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
                ct);
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
        CancellationToken ct)
    {
        var total = (to.Date - from.Date).Days + 1;
        var done = 0;

        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();

            var ok = await CalculateCompanyWideAsync(day, day, triggeredBy, ct);
            if (!ok)
            {
                _logger.LogWarning(
                    "[HRM_ATTENDANCE_WORKER] Dừng tại {Day}: đã xong {Done}/{Total} ngày. Lần sau sẽ tiếp tục từ ngày này.",
                    day.ToString("dd/MM/yyyy"), done, total);
                return;
            }

            done++;
            _logger.LogInformation(
                "[HRM_ATTENDANCE_WORKER] Tiến độ {Done}/{Total} ngày (vừa xong {Day}).",
                done, total, day.ToString("dd/MM/yyyy"));
        }
    }

    private async Task<bool> CalculateCompanyWideAsync(
        DateTime from,
        DateTime to,
        string triggeredBy,
        CancellationToken ct)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var calculation = scope.ServiceProvider.GetRequiredService<IHrmAttendanceCalculationService>();

            var result = await calculation.CalculateAsync(
                new FVN_REGISTER.Contract.Dtos.HrmSync.HrmAttendanceCalculationRequestDto
                {
                    // NULL = all active HRM employees/company-wide.
                    DeptCode = null,
                    EmployeeCode = null,
                    FromDate = from,
                    ToDate = to,
                    ReopenPayrollPeriod = true
                },
                triggeredBy,
                ct);

            if (result.IsSuccess && result.Data is not null)
            {
                _health.Success(nameof(HrmAttendanceCalculationWorker));
                _logger.LogInformation(
                    "[HRM_ATTENDANCE_WORKER] Calculation OK {From} -> {To}: {Employees} nhân viên / {Rows} dòng. Batch={BatchId}",
                    from.ToString("dd/MM/yyyy"),
                    to.ToString("dd/MM/yyyy"),
                    result.Data.EmployeeCount,
                    result.Data.CalculatedRows,
                    result.Data.CalculationBatchId);
                return true;
            }

            _health.Failure(
                nameof(HrmAttendanceCalculationWorker),
                new InvalidOperationException(result.Message ?? "Attendance calculation failed."));
            _logger.LogWarning(
                "[HRM_ATTENDANCE_WORKER] Calculation failed {From} -> {To}: {Message}",
                from.ToString("dd/MM/yyyy"),
                to.ToString("dd/MM/yyyy"),
                result.Message ?? "Unknown error");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[HRM_ATTENDANCE_WORKER] Calculation bị hủy {From} -> {To}.",
                from.ToString("dd/MM/yyyy"),
                to.ToString("dd/MM/yyyy"));
            return false;
        }
        catch (Exception ex)
        {
            _health.Failure(nameof(HrmAttendanceCalculationWorker), ex);
            _logger.LogError(
                ex,
                "[HRM_ATTENDANCE_WORKER] Lỗi khi tính {From} -> {To}.",
                from.ToString("dd/MM/yyyy"),
                to.ToString("dd/MM/yyyy"));
            return false;
        }
    }

    /// <summary>
    /// Marks company-wide worker Run rows that are still 'Running' after BackgroundWorkers:StaleMinutes
    /// as Failed. Only called while this process holds the company-run guard, so no live worker
    /// calculation can own those rows (the process that did was restarted or killed and the proc's
    /// CATCH block never ran). Manual / calendar runs are never touched.
    /// </summary>
    private async Task AbandonStaleRunsAsync(CancellationToken ct)
    {
        var staleMinutes = Math.Clamp(
            _configuration.GetValue<int?>("BackgroundWorkers:StaleMinutes") ?? 15,
            1,
            1440);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();

        var affected = await db.Database.ExecuteSqlAsync($"""
            UPDATE dbo.F03HrmAttendanceCalculationRun
            SET Status=N'Failed',
                FinishedAt=GETDATE(),
                ErrorMessage=N'Abandoned: tiến trình đã dừng trước khi hoàn tất (quá hạn StaleMinutes).'
            WHERE Status=N'Running'
              AND DeptCode IS NULL
              AND EmployeeCode IS NULL
              AND TriggeredBy LIKE N'HRM-ATTENDANCE-WORKER%'
              AND StartedAt < DATEADD(minute, {-staleMinutes}, GETDATE())
        """, ct);

        if (affected > 0)
            _logger.LogWarning(
                "[HRM_ATTENDANCE_WORKER] Đã đóng {Count} dòng Run 'Running' quá {Minutes} phút (process cũ đã dừng).",
                affected, staleMinutes);
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

        var last = await db.Database.SqlQuery<DateTime?>($"""
            SELECT MAX(CONVERT(datetime2, r.ToDate)) AS Value
            FROM dbo.F03HrmAttendanceCalculationRun AS r
            WHERE r.Status=N'Succeeded'
              AND r.DeptCode IS NULL
              AND r.EmployeeCode IS NULL
        """).SingleOrDefaultAsync(ct);

        if (last.HasValue)
            return last.Value.Date.AddDays(1);

        return await GetPayrollPeriodStartAsync(yesterday, ct);
    }

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