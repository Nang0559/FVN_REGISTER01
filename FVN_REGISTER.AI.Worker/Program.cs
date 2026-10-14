using FVN_REGISTER.AI.Infrastructure;
var builder=Host.CreateApplicationBuilder(args);
builder.Services.AddFvnAiInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
internal sealed class Worker(ILogger<Worker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FVN AI Worker started. Processing remains disabled until durable queue and provider adapters are configured.");
        try { await Task.Delay(Timeout.InfiniteTimeSpan,stoppingToken); }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
    }
}