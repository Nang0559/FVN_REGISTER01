using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointCredentialCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EndpointCredentialCleanupHostedService> _logger;
    public EndpointCredentialCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<EndpointCredentialCleanupHostedService> logger){_scopeFactory=scopeFactory;_logger=logger;}

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(5));
        while(await timer.WaitForNextTickAsync(stoppingToken)) await CleanupAsync(stoppingToken);
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            using var scope=_scopeFactory.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
            var revoked=await db.Database.ExecuteSqlRawAsync("UPDATE dbo.F03EndpointCredentials SET RevokedAtUtc=SYSUTCDATETIME() WHERE RevokedAtUtc IS NULL AND GraceExpiresAtUtc IS NOT NULL AND GraceExpiresAtUtc<=SYSUTCDATETIME();",ct);
            if(revoked>0)_logger.LogInformation("Revoked {Count} endpoint credentials whose rotation grace window expired.",revoked);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){}
        catch(Exception ex){_logger.LogError(ex,"Endpoint credential grace cleanup failed.");}
    }
}
