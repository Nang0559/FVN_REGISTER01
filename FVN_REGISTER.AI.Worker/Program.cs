using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
var builder=Host.CreateApplicationBuilder(args);
builder.Services.AddFvnAiInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
internal sealed class Worker(IServiceScopeFactory scopes,IOptions<AIPlatformOptions> options,ILogger<Worker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  var delay=TimeSpan.FromSeconds(Math.Clamp(options.Value.QueuePollSeconds,5,300));
  logger.LogInformation("FVN AI ingestion monitor started. Poll interval {Seconds}s.",delay.TotalSeconds);
  while(!stoppingToken.IsCancellationRequested)
  {
   try
   {
    using var scope=scopes.CreateScope();
    var db=scope.ServiceProvider.GetService<AiPlatformDbContext>();
    if(db is null) logger.LogWarning("AI SQL connection is not configured; queue monitor is idle.");
    else if(await db.Database.CanConnectAsync(stoppingToken))
    {
     var pending=await db.IngestionJobs.CountAsync(x=>x.Status=="Queued",stoppingToken);
     logger.LogDebug("AI ingestion queue contains {PendingJobs} queued jobs.",pending);
    }
    else logger.LogWarning("AI SQL database is unavailable; queue monitor will retry.");
   }
   catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
   catch(Exception ex){logger.LogError(ex,"AI queue monitor iteration failed.");}
   await Task.Delay(delay,stoppingToken);
  }
 }
}