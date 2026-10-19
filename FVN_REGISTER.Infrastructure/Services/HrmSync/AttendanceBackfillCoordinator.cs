using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

/// <summary>
/// Singleton coordinator for per-employee calendar backfill. The SQL job table owns execution
/// and recovery; this coordinator only ensures coverage, enqueues missing ranges, and waits briefly
/// so calendar requests never become the lifetime of a long SQL calculation.
/// </summary>
public sealed class AttendanceBackfillCoordinator : IAttendanceBackfillCoordinator
{
    private sealed record InFlight(DateOnly From, DateOnly To, Task<string?> Completion);
    private sealed record CoverageRun(DateOnly FromDate, DateOnly ToDate);
    private sealed record DateRange(DateOnly From, DateOnly To);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AttendanceBackfillCoordinator> _logger;
    private readonly TimeSpan _maxWait;
    private readonly TimeSpan _backgroundTimeout;
    private readonly object _gate = new();
    private readonly Dictionary<string, InFlight> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    public AttendanceBackfillCoordinator(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IConfiguration configuration,
        ILogger<AttendanceBackfillCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
        _maxWait = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue<int?>("Calendar:AttendanceBackfill:WaitSeconds") ?? 3, 0, 30));
        _backgroundTimeout = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue<int?>("Calendar:AttendanceBackfill:BackgroundTimeoutSeconds") ?? 300, 10, 3600));
    }

    public async Task<AttendanceBackfillResult> EnsureAsync(
        string employeeCode,
        int? deptCode,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var key = employeeCode.Trim();
        if (key.Length == 0 || from > to)
            return new AttendanceBackfillResult(AttendanceBackfillStatus.Failed, "Mã nhân viên hoặc khoảng ngày backfill không hợp lệ.");

        Task<string?> completion;
        lock (_gate)
        {
            if (_inFlight.TryGetValue(key, out var existing))
            {
                if (from < existing.From || to > existing.To)
                    return new AttendanceBackfillResult(AttendanceBackfillStatus.Pending);
                completion = existing.Completion;
            }
            else
            {
                completion = Task.Run(() => RunAsync(key, deptCode, from, to));
                _inFlight[key] = new InFlight(from, to, completion);
            }
        }

        var finished = await Task.WhenAny(completion, Task.Delay(_maxWait, ct));
        if (!ReferenceEquals(finished, completion))
        {
            ct.ThrowIfCancellationRequested();
            return new AttendanceBackfillResult(AttendanceBackfillStatus.Pending);
        }

        var error = await completion;
        return error is null
            ? new AttendanceBackfillResult(AttendanceBackfillStatus.Ready)
            : new AttendanceBackfillResult(AttendanceBackfillStatus.Failed, error);
    }

    private async Task<string?> RunAsync(string employeeCode, int? deptCode, DateOnly from, DateOnly to)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
            timeout.CancelAfter(_backgroundTimeout);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
            var jobs = scope.ServiceProvider.GetRequiredService<IHrmAttendanceCalculationJobService>();

            var effectiveTo = to > DateOnly.FromDateTime(DateTime.Today)
                ? DateOnly.FromDateTime(DateTime.Today)
                : to;
            if (from > effectiveTo)
                return null;

            // Re-check coverage after every completed job. This handles overlapping jobs submitted
            // by another API instance without recalculating already successful dates.
            while (!timeout.IsCancellationRequested)
            {
                var runs = await db.Database.SqlQuery<CoverageRun>($"""
                    SELECT r.FromDate, r.ToDate
                    FROM dbo.F03HrmAttendanceCalculationRun AS r
                    WHERE r.Status=N'Succeeded'
                      AND r.CalculationVersion=N'HRM-PORT-1.1-C3-OVERNIGHT'
                      AND r.FromDate<={effectiveTo}
                      AND r.ToDate>={from}
                      AND (
                           r.EmployeeCode={employeeCode}
                           OR (r.EmployeeCode IS NULL AND (r.DeptCode IS NULL OR r.DeptCode={deptCode}))
                      )
                    ORDER BY r.FromDate
                    """).ToListAsync(timeout.Token);

                var gaps = FindGaps(from, effectiveTo, runs);
                if (gaps.Count == 0)
                    return null;

                var lockedPeriods = await db.PayrollCalculationPeriods.AsNoTracking()
                    .Where(x => x.IsActive != false
                        && (x.Status == "Locked" || x.Status == "Exported")
                        && x.FromDate <= effectiveTo
                        && x.ToDate >= from)
                    .Select(x => new DateRange(x.FromDate, x.ToDate))
                    .ToListAsync(timeout.Token);

                var missing = SubtractRanges(gaps, lockedPeriods);
                if (missing.Count == 0)
                    return null;

                var gap = missing[0];
                // The job service caps each job at 93 calendar dates.
                var chunkTo = gap.To > gap.From.AddDays(92) ? gap.From.AddDays(92) : gap.To;
                var request = new HrmAttendanceCalculationJobCommandDto
                {
                    DeptCode = deptCode,
                    EmployeeCode = employeeCode,
                    FromDate = gap.From.ToDateTime(TimeOnly.MinValue),
                    ToDate = chunkTo.ToDateTime(TimeOnly.MinValue),
                    ReopenPayrollPeriod = false
                };

                var enqueue = await jobs.EnqueueAsync(
                    request,
                    $"CALENDAR-BACKFILL:{employeeCode}",
                    CancellationToken.None,
                    skipCoveredDates: true);

                if (!enqueue.IsSuccess || enqueue.Data is null)
                {
                    var message = enqueue.Message ?? "Không thể đưa backfill chấm công vào hàng đợi.";
                    _logger.LogWarning(
                        "Attendance calendar backfill enqueue failed. EmployeeCode={EmployeeCode}, From={From}, To={To}, Message={Message}",
                        employeeCode, gap.From, chunkTo, message);
                    return message;
                }

                var jobId = enqueue.Data.JobId;
                _logger.LogInformation(
                    "Attendance calendar backfill job ensured. EmployeeCode={EmployeeCode}, JobId={JobId}, From={From}, To={To}",
                    employeeCode, jobId, gap.From, chunkTo);

                while (!timeout.IsCancellationRequested)
                {
                    var current = await jobs.GetAsync(jobId, timeout.Token);
                    if (!current.IsSuccess || current.Data is null)
                        return current.Message ?? $"Không đọc được trạng thái backfill job {jobId:N}.";

                    switch (current.Data.Status)
                    {
                        case "Completed":
                            // Re-query successful runs and only enqueue remaining gaps.
                            goto NextCoverageCheck;
                        case "Failed":
                        case "Cancelled":
                        case "Terminated":
                            return current.Data.ErrorMessage
                                ?? $"Backfill chấm công bị dừng với trạng thái {current.Data.Status}.";
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);
                }

                break;

                NextCoverageCheck: ;
            }

            // The durable SQL job remains queued/running and can be rejoined by a later calendar
            // request. Timeout of this lightweight coordinator never cancels the persistent job.
            _logger.LogWarning(
                "Attendance backfill coordinator wait timed out; persistent job remains recoverable. EmployeeCode={EmployeeCode}, From={From}, To={To}",
                employeeCode, from, effectiveTo);
            return null;
        }
        catch (OperationCanceledException) when (!_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Attendance backfill wait budget elapsed; persistent SQL job remains recoverable. EmployeeCode={EmployeeCode}, From={From}, To={To}",
                employeeCode, from, to);
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Attendance backfill coordinator stopped with the application. EmployeeCode={EmployeeCode}, From={From}, To={To}",
                employeeCode, from, to);
            return "Ứng dụng đang khởi động lại; backfill sẽ được kiểm tra và tiếp tục khi người dùng tải lại lịch.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Attendance calendar backfill coordinator failed. EmployeeCode={EmployeeCode}, From={From}, To={To}",
                employeeCode, from, to);
            return "Không thể chuẩn bị dữ liệu chấm công cho lịch.";
        }
        finally
        {
            lock (_gate)
                _inFlight.Remove(employeeCode);
        }
    }

    private static IReadOnlyList<DateRange> FindGaps(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<CoverageRun> runs)
    {
        var intervals = runs.Select(x => new DateRange(
                x.FromDate < from ? from : x.FromDate,
                x.ToDate > to ? to : x.ToDate))
            .Where(x => x.From <= x.To)
            .OrderBy(x => x.From)
            .ToList();

        var gaps = new List<DateRange>();
        var cursor = from;
        foreach (var interval in intervals)
        {
            if (interval.From > cursor)
                gaps.Add(new DateRange(cursor, interval.From.AddDays(-1)));
            if (interval.To >= cursor)
                cursor = interval.To.AddDays(1);
            if (cursor > to)
                break;
        }
        if (cursor <= to)
            gaps.Add(new DateRange(cursor, to));
        return gaps;
    }

    private static IReadOnlyList<DateRange> SubtractRanges(
        IReadOnlyList<DateRange> source,
        IReadOnlyList<DateRange> exclusions)
    {
        var result = new List<DateRange>();
        foreach (var range in source)
        {
            var pieces = new List<DateRange> { range };
            foreach (var exclusion in exclusions)
            {
                var next = new List<DateRange>();
                foreach (var piece in pieces)
                {
                    if (exclusion.To < piece.From || exclusion.From > piece.To)
                    {
                        next.Add(piece);
                        continue;
                    }
                    if (piece.From < exclusion.From)
                        next.Add(new DateRange(piece.From, exclusion.From.AddDays(-1)));
                    if (piece.To > exclusion.To)
                        next.Add(new DateRange(exclusion.To.AddDays(1), piece.To));
                }
                pieces = next;
                if (pieces.Count == 0)
                    break;
            }
            result.AddRange(pieces);
        }
        return result.OrderBy(x => x.From).ToList();
    }
}
