using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Worker;

internal sealed class RetentionWorker(
    IServiceScopeFactory scopes,
    IOptions<AIPlatformOptions> options,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Value.RetentionPollMinutes, 5, 1440));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetService<AiPlatformDbContext>();
                if (db is null)
                {
                    logger.LogWarning("AI SQL connection string is not configured; retention worker is idle.");
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }
                if (!await db.Database.CanConnectAsync(stoppingToken))
                {
                    logger.LogWarning("AI SQL is unavailable; retention worker will retry.");
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }

                var retention = scope.ServiceProvider.GetService<IDocumentRetentionService>();
                if (retention is not null)
                {
                    var result = await retention.ArchiveExpiredAsync(DateTimeOffset.UtcNow, stoppingToken);
                    if (result.ArchivedDocuments > 0)
                        logger.LogInformation("AI retention archived {Archived} documents and purged {Purged} originals.",
                            result.ArchivedDocuments, result.DeletedContent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI document retention cycle failed; the next cycle will retry.");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }
}
