using FVN_REGISTER.Application.Interfaces.HrmSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

public sealed class HrmSourceFreshnessService : IHrmSourceFreshnessService
{
    private readonly FVNWEBAPPContext _db;
    private readonly ILogger<HrmSourceFreshnessService> _logger;
    private readonly bool _enabled;
    private readonly bool _captureForBulkJobs;
    private readonly int _missingStampLookbackDays;

    private sealed record StaleRow(string EmployeeCode, DateOnly FromDate, DateOnly ToDate);

    public HrmSourceFreshnessService(
        FVNWEBAPPContext db,
        IConfiguration configuration,
        ILogger<HrmSourceFreshnessService> logger)
    {
        _db = db;
        _logger = logger;
        _enabled = configuration.GetValue<bool?>("HrmAttendanceFreshness:Enabled") ?? true;
        _captureForBulkJobs = configuration.GetValue<bool?>("HrmAttendanceFreshness:CaptureForBulkJobs") ?? true;
        // Dates calculated before this feature existed have no fingerprint. Only dates inside this
        // window are treated as stale for that reason, so deploying never triggers a recalculation storm.
        _missingStampLookbackDays = Math.Clamp(
            configuration.GetValue<int?>("HrmAttendanceFreshness:MissingStampLookbackDays") ?? 45, 0, 366);
    }

    public async Task<HrmSourceCapture> CaptureAsync(string? employeeCode, DateOnly workDate, CancellationToken ct = default)
    {
        var employee = string.IsNullOrWhiteSpace(employeeCode) ? null : employeeCode.Trim();
        var capture = new HrmSourceCapture(Guid.NewGuid(), workDate, employee, false);
        if (!_enabled || (employee is null && !_captureForBulkJobs))
            return capture;

        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.usp_CaptureHrmAttendanceSourceStamp @WorkDate={workDate}, @CaptureId={capture.CaptureId}, @EmployeeCode={employee}",
                ct);
            return capture with { IsActive = true };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[HRM_FRESHNESS] Capture failed; date will be re-checked later. EmployeeCode={EmployeeCode}, WorkDate={WorkDate}",
                employee, workDate);
            return capture;
        }
    }

    public async Task CommitAsync(HrmSourceCapture capture, Guid? calculationBatchId, CancellationToken ct = default)
    {
        if (!capture.IsActive)
            return;

        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.usp_CommitHrmAttendanceSourceStamp @WorkDate={capture.WorkDate}, @CaptureId={capture.CaptureId}, @CalculationBatchId={calculationBatchId}, @EmployeeCode={capture.EmployeeCode}",
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not committing is the safe direction: the date simply looks stale and is re-checked.
            _logger.LogWarning(ex,
                "[HRM_FRESHNESS] Commit failed. EmployeeCode={EmployeeCode}, WorkDate={WorkDate}",
                capture.EmployeeCode, capture.WorkDate);
        }
    }

    public async Task<IReadOnlyList<DateOnly>> GetStaleDatesAsync(
        string employeeCode, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var employee = employeeCode?.Trim();
        if (!_enabled || string.IsNullOrEmpty(employee) || from > to)
            return Array.Empty<DateOnly>();

        var floor = DateOnly.FromDateTime(DateTime.Today).AddDays(-_missingStampLookbackDays);
        try
        {
            return await _db.Database.SqlQuery<DateOnly>($"""
                SELECT c.WorkDate AS [Value]
                FROM dbo.fn_HrmAttendanceSourceStamp({employee},{from},{to}) AS c
                LEFT JOIN dbo.F03HrmAttendanceSourceStamp AS s
                       ON s.EmployeeCode={employee} AND s.WorkDate=c.WorkDate
                WHERE (s.SourceStamp IS NOT NULL AND s.SourceStamp<>c.SourceStamp)
                   OR (s.SourceStamp IS NULL AND c.WorkDate>={floor})
                ORDER BY c.WorkDate
                """).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[HRM_FRESHNESS] Stale check unavailable (is SQL/104 deployed?); falling back to coverage only. EmployeeCode={EmployeeCode}",
                employee);
            return Array.Empty<DateOnly>();
        }
    }

    public async Task<IReadOnlyList<HrmStaleEmployeeRange>> GetStaleEmployeeRangesAsync(
        DateOnly from, DateOnly to, int maxEmployees, CancellationToken ct = default)
    {
        if (!_enabled || from > to)
            return Array.Empty<HrmStaleEmployeeRange>();

        var top = Math.Clamp(maxEmployees, 1, 500);
        try
        {
            // Only employees that already carry a committed fingerprint are swept, so the first
            // deployment cannot enqueue the whole company.
            var rows = await _db.Database.SqlQuery<StaleRow>($"""
                SELECT TOP ({top}) e.EmployeeCode AS EmployeeCode, MIN(c.WorkDate) AS FromDate, MAX(c.WorkDate) AS ToDate
                FROM (SELECT DISTINCT EmployeeCode FROM dbo.F03HrmAttendanceSourceStamp
                      WHERE WorkDate BETWEEN {from} AND {to} AND SourceStamp IS NOT NULL) AS e
                CROSS APPLY dbo.fn_HrmAttendanceSourceStamp(e.EmployeeCode,{from},{to}) AS c
                INNER JOIN dbo.F03HrmAttendanceSourceStamp AS s
                        ON s.EmployeeCode=e.EmployeeCode AND s.WorkDate=c.WorkDate
                WHERE s.SourceStamp IS NOT NULL AND s.SourceStamp<>c.SourceStamp
                GROUP BY e.EmployeeCode
                ORDER BY MIN(c.WorkDate)
                """).ToListAsync(ct);
            return rows.Select(x => new HrmStaleEmployeeRange(x.EmployeeCode, x.FromDate, x.ToDate)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[HRM_FRESHNESS] Stale sweep query unavailable (is SQL/104 deployed?).");
            return Array.Empty<HrmStaleEmployeeRange>();
        }
    }
}
