using FVN_REGISTER.Application.Interfaces.Common;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Leaves;
using FVN_REGISTER.Application.Interfaces.OT;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs
{
    /// <summary>
    /// Runs the shared approval-timeout pipeline for every supported request module.
    /// The worker interval is intentionally shorter than the escalation threshold so
    /// a request is not left waiting for hours after its rule has expired.
    /// </summary>
    public class EscalationBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EscalationBackgroundWorker> _logger;
        private const string JobKey = BackgroundJobCatalog.Escalation;
        private readonly IBackgroundJobScheduler _scheduler;

        public EscalationBackgroundWorker(
            IServiceProvider serviceProvider,
            ILogger<EscalationBackgroundWorker> logger,
            IBackgroundJobScheduler scheduler)
        {
            _scheduler = scheduler;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ESC_WORKER] Started (schedule is read from F03BackgroundJobSchedules)");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await _scheduler.WaitUntilEnabledAsync(JobKey, stoppingToken);
                    await DoWorkAsync(stoppingToken);
                    await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // App đang shutdown.
            }

            _logger.LogInformation("[ESC_WORKER] Stopped");
        }

        private async Task DoWorkAsync(CancellationToken ct)
        {
            try
            {
                _scheduler.MarkStarted(JobKey);
                _logger.LogInformation("[ESC_WORKER] Running at {Time}", DateTimeOffset.Now);

                await using var scope = _serviceProvider.CreateAsyncScope();

                var leaveEscalation = scope.ServiceProvider.GetRequiredService<ILeaveEscalationService>();
                await leaveEscalation.ProcessAsync(ct);

                var otEscalation = scope.ServiceProvider.GetRequiredService<IOTEscalationService>();
                await otEscalation.ProcessAsync(ct);

                _logger.LogInformation("[ESC_WORKER] Completed at {Time}", DateTimeOffset.Now);
                _scheduler.MarkSucceeded(JobKey);
            }
            catch (OperationCanceledException)
            {
                // Bỏ qua khi shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ESC_WORKER] Error during execution");
                _scheduler.MarkFailed(JobKey, ex.Message);
            }
        }
    }
}
