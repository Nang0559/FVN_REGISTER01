using FVN_REGISTER.Application.Interfaces.HrmSync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

/// <summary>
/// Singleton. Runs IHrmAttendanceCalculationService.EnsureEmployeeRangeAsync detached from the HTTP request:
/// - the caller waits at most Calendar:AttendanceBackfill:WaitSeconds (default 3) and then gets
///   whatever data already exists;
/// - the backfill keeps running in its own DI scope, bounded by
///   Calendar:AttendanceBackfill:BackgroundTimeoutSeconds (default 300) and application shutdown;
/// - at most one backfill per employee is in flight; repeated calendar refreshes join it instead of
///   piling up more exclusive date locks in usp_CalculateHrmAttendance.
/// </summary>
public sealed class AttendanceBackfillCoordinator : IAttendanceBackfillCoordinator
{
    private sealed record InFlight(DateOnly From, DateOnly To, Task<string?> Completion);

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
        Task<string?> completion;

        lock (_gate)
        {
            if (_inFlight.TryGetValue(key, out var existing))
            {
                // A backfill for this employee is already running. Join it only if it covers this
                // range; otherwise do not start a second one in parallel (the next request will).
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

        var error = await completion; // RunAsync never throws; it returns the error message or null.
        return error is null
            ? new AttendanceBackfillResult(AttendanceBackfillStatus.Ready)
            : new AttendanceBackfillResult(AttendanceBackfillStatus.Failed, error);
    }

    private async Task<string?> RunAsync(string employeeCode, int? deptCode, DateOnly from, DateOnly to)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
            cts.CancelAfter(_backgroundTimeout);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var calculation = scope.ServiceProvider.GetRequiredService<IHrmAttendanceCalculationService>();

            var result = await calculation.EnsureEmployeeRangeAsync(employeeCode, deptCode, from, to, cts.Token);
            if (result.IsSuccess)
                return null;

            var message = result.Message ?? "Không thể backfill chấm công.";
            _logger.LogWarning(
                "Attendance calendar backfill failed. EmployeeCode={EmployeeCode}, From={From}, To={To}, Message={Message}",
                employeeCode, from, to, message);
            return message;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Attendance calendar backfill cancelled (timeout {Timeout}s or shutdown). EmployeeCode={EmployeeCode}, From={From}, To={To}",
                _backgroundTimeout.TotalSeconds, employeeCode, from, to);
            return "Backfill chấm công bị hủy do quá thời gian.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Attendance calendar backfill crashed. EmployeeCode={EmployeeCode}, From={From}, To={To}",
                employeeCode, from, to);
            return "Backfill chấm công gặp lỗi.";
        }
        finally
        {
            lock (_gate)
                _inFlight.Remove(employeeCode);
        }
    }
}
