using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

/// <summary>
/// Automatic background flow: periodically finds employees whose HRM source changed after their
/// attendance was calculated and enqueues low-priority, employee-scoped catch-up jobs. It never
/// calculates anything itself and never enqueues a company-wide job; execution, progress, cancel and
/// resume stay owned by <see cref="HrmAttendanceCalculationJobWorker"/>. Disabled by default so the
/// SQL fingerprints can be bootstrapped first.
/// </summary>
public sealed class HrmAttendanceStaleSweepWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HrmAttendanceStaleSweepWorker> _logger;
    private readonly BackgroundWorkerHealthRegistry _health;

    public HrmAttendanceStaleSweepWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<HrmAttendanceStaleSweepWorker> logger,
        BackgroundWorkerHealthRegistry health)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _health = health;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var name = nameof(HrmAttendanceStaleSweepWorker);
        _health.Started(name);
        var lastPass = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Heartbeat every minute so the health check does not flag a long sweep interval as stale.
                _health.Success(name);

                var enabled = _configuration.GetValue<bool?>("HrmAttendanceFreshness:SweepEnabled") ?? false;
                var interval = TimeSpan.FromMinutes(Math.Clamp(
                    _configuration.GetValue<int?>("HrmAttendanceFreshness:SweepIntervalMinutes") ?? 60, 5, 1440));

                if (enabled && DateTimeOffset.UtcNow - lastPass >= interval)
                {
                    await SweepOnceAsync(stoppingToken);
                    lastPass = DateTimeOffset.UtcNow;
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _health.Failure(name, ex);
                _logger.LogError(ex, "[HRM_FRESHNESS_SWEEP] Sweep pass failed.");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        var lookbackDays = Math.Clamp(
            _configuration.GetValue<int?>("HrmAttendanceFreshness:SweepLookbackDays") ?? 14, 1, 31);
        var maxEmployees = Math.Clamp(
            _configuration.GetValue<int?>("HrmAttendanceFreshness:SweepMaxEmployeesPerPass") ?? 50, 1, 500);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-lookbackDays);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var freshness = scope.ServiceProvider.GetRequiredService<IHrmSourceFreshnessService>();
        var jobs = scope.ServiceProvider.GetRequiredService<IHrmAttendanceCalculationJobService>();

        var stale = await freshness.GetStaleEmployeeRangesAsync(from, today, maxEmployees, ct);
        var queued = 0;
        foreach (var item in stale)
        {
            ct.ThrowIfCancellationRequested();
            var result = await jobs.EnqueueAsync(
                new HrmAttendanceCalculationJobCommandDto
                {
                    EmployeeCode = item.EmployeeCode,
                    FromDate = item.FromDate.ToDateTime(TimeOnly.MinValue),
                    ToDate = item.ToDate.ToDateTime(TimeOnly.MinValue),
                    ReopenPayrollPeriod = false // locked/exported payroll periods keep their protection
                },
                "STALE-SWEEP",
                ct,
                skipCoveredDates: true, // the job worker removes stale dates from the covered set itself
                priority: 2);
            if (result.IsSuccess)
                queued++;
            else
                _logger.LogDebug("[HRM_FRESHNESS_SWEEP] Enqueue skipped for {EmployeeCode}: {Message}", item.EmployeeCode, result.Message);
        }

        if (stale.Count > 0)
            _logger.LogInformation("[HRM_FRESHNESS_SWEEP] Stale employees={Stale}, jobs queued/joined={Queued}.", stale.Count, queued);
    }
}
