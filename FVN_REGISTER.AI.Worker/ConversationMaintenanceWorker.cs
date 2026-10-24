using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Worker;

/// <summary>
/// Runs the SQL-backed recovery and retention procedures for persisted conversations.
/// These procedures are inert unless this worker or an external scheduler invokes them.
/// </summary>
internal sealed class ConversationMaintenanceWorker(
    IServiceScopeFactory scopes,
    IOptions<AIPlatformOptions> options,
    ILogger<ConversationMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Value.ConversationMaintenancePollMinutes, 1, 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetService<AiPlatformDbContext>();

                if (db is not null && await db.Database.CanConnectAsync(stoppingToken))
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC ai.usp_RecoverStaleConversationTurns @StaleMinutes={15}, @BatchSize={200}",
                        stoppingToken);
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC ai.usp_PurgeExpiredConversations @BatchSize={200}",
                        stoppingToken);
                    logger.LogDebug("AI conversation recovery and expiry-purge procedures completed.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI conversation maintenance failed; the next scheduled cycle will retry.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
