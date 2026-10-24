using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Worker;

internal sealed class ConnectorWorker(
    IServiceScopeFactory scopes,
    IOptions<AIPlatformOptions> options,
    ILogger<ConnectorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Value.ConnectorPollMinutes, 1, 1440));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetService<AiPlatformDbContext>();
                if (db is null)
                {
                    logger.LogWarning("AI SQL connection string is not configured; connector worker is idle.");
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }
                if (!await db.Database.CanConnectAsync(stoppingToken))
                {
                    logger.LogWarning("AI SQL is unavailable; connector worker will retry.");
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }

                var connector = scope.ServiceProvider.GetService<IDocumentSourceConnector>();
                if (connector is not null && options.Value.FolderConnectors.Any(x => x.Enabled))
                {
                    var count = await connector.IngestAvailableAsync(stoppingToken);
                    if (count > 0) logger.LogInformation("AI folder connectors ingested {Count} new documents.", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "AI connector polling cycle failed; the next cycle will retry."); }
            await Task.Delay(interval, stoppingToken);
        }
    }
}
