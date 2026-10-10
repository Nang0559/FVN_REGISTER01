using Microsoft.AspNetCore.Routing;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Core.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class SecurityFunctionDiscoveryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SecurityFunctionDiscoveryHostedService> _logger;
    private readonly IBackgroundJobScheduler _scheduler;
    private const string JobKey = BackgroundJobCatalog.SecurityFunctionDiscovery;
    private DateOnly? _lastCleanupLocalDate;

    public SecurityFunctionDiscoveryHostedService(IServiceScopeFactory scopeFactory, ILogger<SecurityFunctionDiscoveryHostedService> logger, IBackgroundJobScheduler scheduler)
    {
        _scheduler = scheduler;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var failed = false;
            try
            {
                await _scheduler.WaitUntilEnabledAsync(JobKey, stoppingToken);
                _scheduler.MarkStarted(JobKey);
                using var scope = _scopeFactory.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<SecurityFunctionRegistryService>();
                var result = await registry.ReconcileAsync(stoppingToken);

                var db = scope.ServiceProvider.GetRequiredService<FVN_REGISTER.Infrastructure.FVNWEBAPPContext>();
                var endpointSources = scope.ServiceProvider.GetServices<EndpointDataSource>();
                var candidateDiscovery = new SecurityCandidateDiscovery(db, endpointSources);
                var candidates = await candidateDiscovery.ScanAsync(stoppingToken);

                _logger.LogInformation(
                    "Security function discovery completed: Discovered={Discovered}, Matched={Matched}, New={New}, Retirement={Retirement}, Conflict={Conflict}, Candidates={Candidates}",
                    result.Discovered, result.Matched, result.PendingRegistration, result.PendingRetirement, result.Conflict, candidates);

                await RunDailyEndpointComplianceCleanupAsync(db, stoppingToken);
                _scheduler.MarkSucceeded(JobKey);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                failed = true;
                _scheduler.MarkFailed(JobKey, ex.Message);
                _logger.LogError(ex, "Security function discovery failed. Existing permissions were not changed by the discovery process. Retrying in 5 minutes.");
            }

            try
            {
                // After a failure retry quickly (5 min) like before; otherwise follow the configured schedule.
                if (failed) await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                else await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunDailyEndpointComplianceCleanupAsync(FVN_REGISTER.Infrastructure.FVNWEBAPPContext db, CancellationToken cancellationToken)
    {
        var localNow = GetVietnamNow();
        if (localNow.Hour < 3 || _lastCleanupLocalDate == DateOnly.FromDateTime(localNow.Date)) return;

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.usp_CleanupEndpointComplianceFindings @RetentionDays={0}, @BatchSize={1}",
                new object[] { 180, 5000 }, cancellationToken);
            _lastCleanupLocalDate = DateOnly.FromDateTime(localNow.Date);
            _logger.LogInformation("Endpoint compliance finding cleanup completed at Vietnam local time {LocalTime}.", localNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Endpoint compliance finding cleanup failed at Vietnam local time {LocalTime}.", localNow);
        }
    }

    private static DateTime GetVietnamNow()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
    }
}
