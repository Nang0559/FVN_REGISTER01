using FVN_REGISTER.Application.Interfaces.Common;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs
{
    public class EmailBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailBackgroundWorker> _logger;
        private const string JobKey = BackgroundJobCatalog.EmailQueue;
        private readonly IBackgroundJobScheduler _scheduler;
        private readonly BackgroundWorkerHealthRegistry _health;
        private readonly Polly.ResiliencePipeline _retry;

        public EmailBackgroundWorker(
            IServiceProvider serviceProvider,
            ILogger<EmailBackgroundWorker> logger,
            BackgroundWorkerHealthRegistry health,
            IBackgroundJobScheduler scheduler)
        {
            _scheduler = scheduler;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _health = health;
            _retry = JobRetryPolicy.Create("email-queue", logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Background Worker started");
            _health.Started(nameof(EmailBackgroundWorker));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _scheduler.WaitUntilEnabledAsync(JobKey, stoppingToken);
                    _scheduler.MarkStarted(JobKey);

                    _logger.LogInformation(
                        "Email queue processing at {Time}",
                        DateTimeOffset.Now);

                    await _retry.ExecuteAsync(async token =>
                    {
                        await using var scope = _serviceProvider.CreateAsyncScope();
                        var emailService = scope.ServiceProvider
                            .GetRequiredService<IEmailService>();
                        await emailService.ProcessQueue(token);
                    }, stoppingToken);

                    _health.Success(nameof(EmailBackgroundWorker));
                    _scheduler.MarkSucceeded(JobKey);
                }
                catch (OperationCanceledException)
                {
                    break; // Không log vì app đang shutdown
                }
                catch (ObjectDisposedException)
                {
                    break; // Logger đã disposed, không log
                }
                catch (Exception ex)
                {
                    // Dùng Console thay Logger để tránh ObjectDisposedException
                    _health.Failure(nameof(EmailBackgroundWorker), ex);
                    _scheduler.MarkFailed(JobKey, ex.Message);
                    try
                    {
                        _logger.LogError(ex, "Error executing email queue after retry policy");
                    }
                    catch
                    {
                        Console.WriteLine($"[EMAIL WORKER] {ex.Message}");
                    }
                }

                // Delay an toàn
                try
                {
                    await _scheduler.WaitForNextAsync(JobKey, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                _logger.LogInformation("Email Background Worker stopped");
            }
            catch
            {
                Console.WriteLine("[EMAIL WORKER] Stopped");
            }
        }
    }

}
