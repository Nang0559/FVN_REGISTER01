using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FVN_REGISTER.AI.Application.Configuration;

namespace FVN_REGISTER.AI.Worker;

/// <summary>Durable worker for document-to-Excel extraction. It never performs an operational business write.</summary>
public sealed class DataEntryWorker(IServiceScopeFactory scopes, IOptions<AIPlatformOptions> options, ILogger<DataEntryWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"data-entry:{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(Math.Clamp(options.Value.QueuePollSeconds, 2, 300)); logger.LogInformation("FVN AI data-entry worker {WorkerId} started.", _workerId);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetService<AiPlatformDbContext>();
                if (db is null) { await Task.Delay(poll, stoppingToken); continue; }
                if (!await db.Database.CanConnectAsync(stoppingToken)) { logger.LogWarning("AI SQL is unavailable; data-entry worker will retry."); await Task.Delay(poll, stoppingToken); continue; }
                await RecoverExpiredAsync(db, stoppingToken);
                var jobs = new List<AiDataEntryRunRecord>(); for (var i = 0; i < Math.Clamp(options.Value.MaximumConcurrentJobs, 1, 8); i++) { var job = await ClaimAsync(db, stoppingToken); if (job is null) break; jobs.Add(job); }
                if (jobs.Count == 0) await Task.Delay(poll, stoppingToken); else await Task.WhenAll(jobs.Select(job => ProcessWithLeaseAsync(job, stoppingToken)));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "AI data-entry worker loop failed."); await Task.Delay(poll, stoppingToken); }
        }
    }

    private async Task ProcessWithLeaseAsync(AiDataEntryRunRecord job, CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); var processing = ProcessCoreAsync(job, linked.Token); var leaseLost = false;
        while (!processing.IsCompleted && !stoppingToken.IsCancellationRequested)
        {
            var completed = await Task.WhenAny(processing, Task.Delay(TimeSpan.FromMinutes(2), stoppingToken)); if (completed == processing) break;
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AiPlatformDbContext>(); var current = await db.DataEntryRuns.SingleOrDefaultAsync(x => x.Id == job.Id, stoppingToken);
                if (current is null || !string.Equals(current.LeaseOwner, _workerId, StringComparison.Ordinal)) { leaseLost = true; linked.Cancel(); break; }
                current.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(10); current.UpdatedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { linked.Cancel(); break; }
            catch (Exception ex) { leaseLost = true; logger.LogError(ex, "Could not renew data-entry lease for run {RunId}.", job.Id); linked.Cancel(); break; }
        }
        try { await processing; }
        catch (OperationCanceledException) when (leaseLost && !stoppingToken.IsCancellationRequested) { logger.LogWarning("Data-entry lease lost for run {RunId}; recovery will make it claimable.", job.Id); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Data-entry processing failed for run {RunId}.", job.Id); await FailAsync(job.Id, ex.Message.Length > 120 ? "DATA_ENTRY_UNEXPECTED_ERROR" : ex.Message); }
    }

    private async Task ProcessCoreAsync(AiDataEntryRunRecord job, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IDocumentDataEntryService>(); await service.ProcessAsync(job.Id, _workerId, cancellationToken);
        using var updateScope = scopes.CreateScope(); var db = updateScope.ServiceProvider.GetRequiredService<AiPlatformDbContext>(); var current = await db.DataEntryRuns.SingleOrDefaultAsync(x => x.Id == job.Id, cancellationToken);
        if (current is not null && string.Equals(current.LeaseOwner, _workerId, StringComparison.Ordinal)) { current.LeaseOwner = null; current.LeaseUntilUtc = null; current.UpdatedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken); }
    }

    private async Task<AiDataEntryRunRecord?> ClaimAsync(AiPlatformDbContext db, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken); var now = DateTimeOffset.UtcNow;
        var job = await db.DataEntryRuns.FromSqlInterpolated($"SELECT TOP (1) * FROM ai.AiDataEntryRuns WITH (UPDLOCK, READPAST, ROWLOCK) WHERE Status = N'WaitingForDocument' AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= {now}) AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc < {now}) ORDER BY CreatedAtUtc ASC").SingleOrDefaultAsync(cancellationToken);
        if (job is null) { await tx.RollbackAsync(cancellationToken); return null; }
        job.Status = "Running"; job.AttemptCount += 1; job.StartedAtUtc ??= now; job.LastAttemptAtUtc = now; job.LeaseOwner = _workerId; job.LeaseUntilUtc = now.AddMinutes(10); job.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return job;
    }

    private static async Task RecoverExpiredAsync(AiPlatformDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await db.DataEntryRuns.Where(x => x.Status == "Running" && x.LeaseUntilUtc.HasValue && x.LeaseUntilUtc < now).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "WaitingForDocument").SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null).SetProperty(x => x.NextAttemptAtUtc, now).SetProperty(x => x.FailureCode, "LEASE_EXPIRED").SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
    }

    private async Task FailAsync(Guid runId, string failureCode)
    {
        try
        {
            using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AiPlatformDbContext>(); var run = await db.DataEntryRuns.SingleOrDefaultAsync(x => x.Id == runId); if (run is null) return;
            var now = DateTimeOffset.UtcNow; var terminal = run.AttemptCount >= run.MaxAttempts; run.Status = terminal ? "Failed" : "WaitingForDocument"; run.FailureCode = failureCode; run.LastErrorAtUtc = now; run.LeaseOwner = null; run.LeaseUntilUtc = null; run.NextAttemptAtUtc = terminal ? null : now.AddSeconds(Math.Min(300, 10 * Math.Pow(2, Math.Min(5, run.AttemptCount - 1)))); run.UpdatedAtUtc = now; await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "Could not persist data-entry failure for run {RunId}; lease recovery will retry.", runId); }
    }
}
