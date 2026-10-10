using FVN_REGISTER.Infrastructure;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Core.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointCredentialCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EndpointCredentialCleanupHostedService> _logger;
    private const string JobKey = BackgroundJobCatalog.EndpointCredentialCleanup;
    private readonly IBackgroundJobScheduler _scheduler;
    public EndpointCredentialCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<EndpointCredentialCleanupHostedService> logger, IBackgroundJobScheduler scheduler){_scopeFactory=scopeFactory;_logger=logger;_scheduler=scheduler;}

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _scheduler.WaitUntilEnabledAsync(JobKey, stoppingToken);
                await CleanupAsync(stoppingToken);
                await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            _scheduler.MarkStarted(JobKey);
            using var scope=_scopeFactory.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
            var revoked=await db.Database.ExecuteSqlRawAsync("UPDATE dbo.F03EndpointCredentials SET RevokedAtUtc=SYSUTCDATETIME() WHERE RevokedAtUtc IS NULL AND GraceExpiresAtUtc IS NOT NULL AND GraceExpiresAtUtc<=SYSUTCDATETIME();",ct);
            if(revoked>0)_logger.LogInformation("Revoked {Count} endpoint credentials whose rotation grace window expired.",revoked);
            _scheduler.MarkSucceeded(JobKey, revoked>0 ? $"Revoked {revoked}" : null);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){}
        catch(Exception ex){_logger.LogError(ex,"Endpoint credential grace cleanup failed.");_scheduler.MarkFailed(JobKey, ex.Message);}
    }
}
