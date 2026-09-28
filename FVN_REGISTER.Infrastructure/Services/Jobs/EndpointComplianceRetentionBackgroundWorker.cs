using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

public sealed class EndpointComplianceRetentionBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EndpointComplianceRetentionBackgroundWorker> _logger;

    public EndpointComplianceRetentionBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<EndpointComplianceRetentionBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var firstRun = DateTime.UtcNow.Date.AddDays(1).AddHours(3).AddMinutes(15) - DateTime.UtcNow;
        if (firstRun < TimeSpan.Zero) firstRun = firstRun.AddDays(1);
        try { await Task.Delay(firstRun, stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FVN_REGISTER.Infrastructure.FVNWEBAPPContext>();
                await db.Database.ExecuteSqlRawAsync("EXEC dbo.usp_CleanupEndpointComplianceFindings @RetentionDays={0}, @BatchSize={1}", new object[] { 180, 5000 }, stoppingToken);
                _logger.LogInformation("Endpoint compliance finding retention cleanup completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Endpoint compliance finding retention cleanup failed."); }

            try { await Task.Delay(TimeSpan.FromDays(1), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
