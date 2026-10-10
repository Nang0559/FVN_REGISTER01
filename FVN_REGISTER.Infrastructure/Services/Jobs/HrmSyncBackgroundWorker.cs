using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Leaves;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs
{
    public sealed class HrmSyncBackgroundWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<HrmSyncBackgroundWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly BackgroundWorkerHealthRegistry _health;
        private readonly Polly.ResiliencePipeline _retry;

        private const string JobKey = BackgroundJobCatalog.HrmSync;
        private readonly IBackgroundJobScheduler _scheduler;

        // Leave entitlement is recalculated only when HRM added employees, or once per day.
        private static DateOnly? _lastEntitlementDate;

        public HrmSyncBackgroundWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<HrmSyncBackgroundWorker> logger,
            IConfiguration configuration,
            BackgroundWorkerHealthRegistry health,
            IBackgroundJobScheduler scheduler)
        {
            _scheduler = scheduler;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
            _health = health;
            _retry = JobRetryPolicy.Create("hrm-sync", logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _health.Started(nameof(HrmSyncBackgroundWorker));

            // HRM sync is intentionally delayed after application startup so that
            // authentication/dashboard traffic is not competing with the first
            // heavy synchronization pass.
            var runOnStartup = _configuration.GetValue<bool>("HrmSync:RunOnStartup");
            if (runOnStartup)
            {
                var startupDelaySeconds = _configuration.GetValue<int?>("HrmSync:StartupDelaySeconds") ?? 30;
                if (startupDelaySeconds > 0)
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(Math.Min(startupDelaySeconds, 3600)),
                            stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
            else
            {
                try
                {
                    await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                string? lastSummary = null;
                try
                {
                    await _scheduler.WaitUntilEnabledAsync(JobKey, stoppingToken);
                    _scheduler.MarkStarted(JobKey);

                    await _retry.ExecuteAsync(async token =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var sync = scope.ServiceProvider.GetRequiredService<IHrmSyncService>();

                        // Automatic HRM polling performs the master-data sync only.
                        // Security provisioning is intentionally not repeated on every
                        // polling cycle; it remains available through the manual Security
                        // reconciliation endpoint/UI and can be enabled explicitly with
                        // HrmSync:SecurityProvisioningOnAutomatic=true when required.
                        var result = await sync.RunAllAsync("SYSTEM", manual: false, token);
                        var run = result.Data;

                        if (!result.IsSuccess || run == null || !run.Success)
                            throw new InvalidOperationException(result.Message ?? run?.Summary ?? "HRM sync failed.");

                        var employeeAdded = run.Jobs.Any(j =>
                            string.Equals(j.EntityType, "Employee", StringComparison.OrdinalIgnoreCase) && j.Added > 0);
                        var today = DateOnly.FromDateTime(DateTime.Today);

                        if (employeeAdded || _lastEntitlementDate != today)
                        {
                            var entitlement = scope.ServiceProvider.GetRequiredService<ILeaveEntitlementService>();
                            var entResult = await entitlement.EnsureWorkYearCalculatedAsync(DateTime.Today.Year, token);
                            if (entResult.IsSuccess)
                                _lastEntitlementDate = today;
                            else
                                _logger.LogWarning(
                                    "[HRM-SYNC] Leave entitlement calculation did not complete: {Message}",
                                    entResult.Message);
                        }

                        lastSummary = run.Summary;
                        _logger.LogInformation(
                            "[HRM-SYNC] Automatic synchronization completed: {Summary}",
                            run.Summary);
                    }, stoppingToken);

                    _health.Success(nameof(HrmSyncBackgroundWorker));
                    _scheduler.MarkSucceeded(JobKey, lastSummary);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _health.Failure(nameof(HrmSyncBackgroundWorker), ex);
                    _scheduler.MarkFailed(JobKey, ex.Message);
                    _logger.LogError(ex, "[HRM-SYNC] Automatic synchronization failed after retry policy.");
                }

                try
                {
                    await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}