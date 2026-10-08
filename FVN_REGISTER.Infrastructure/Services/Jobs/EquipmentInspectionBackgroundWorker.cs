using FVN_REGISTER.Application.Interfaces.Equipment;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Core.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

public sealed class EquipmentInspectionBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EquipmentInspectionBackgroundWorker> _logger;
    private readonly IBackgroundJobScheduler _scheduler;
    private const string JobKey = BackgroundJobCatalog.EquipmentInspection;

    public EquipmentInspectionBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<EquipmentInspectionBackgroundWorker> logger, IBackgroundJobScheduler scheduler)
    {
        _scheduler = scheduler;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _scheduler.WaitUntilEnabledAsync(JobKey, stoppingToken);
                _scheduler.MarkStarted(JobKey);
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IEquipmentInspectionService>();
                await service.GenerateScheduledTasksAsync(stoppingToken);
                _scheduler.MarkSucceeded(JobKey);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Equipment inspection worker failed.");
                _scheduler.MarkFailed(JobKey, ex.Message);
            }

            await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
        }
    }
}