using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Providers;

public sealed class SqlIngestionQueue(AiPlatformDbContext db) : IIngestionQueue, ILeasedIngestionQueue
{
    public async Task EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        // Document and job are committed atomically by DocumentIngestionService.
        // This method is retained for explicit requeue commands; it never creates a second job.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ai.AiIngestionJobs SET Status=N'Queued', NextAttemptAtUtc=TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'), FailureCode=NULL, CompletedAtUtc=NULL WHERE DocumentId={documentId} AND Status IN (N'DeadLetter',N'Failed')",
            cancellationToken);
    }

    public async Task<LeasedIngestionJob?> ClaimAsync(string workerId, int leaseSeconds, CancellationToken cancellationToken = default)
    {
        var rows = await db.Database.SqlQueryRaw<ClaimedJob>(
            "EXEC ai.usp_ClaimAiIngestionJob @WorkerId={0}, @LeaseSeconds={1}", workerId, leaseSeconds)
            .ToListAsync(cancellationToken);
        var row = rows.FirstOrDefault();
        return row is null ? null : new LeasedIngestionJob(row.JobId, row.DocumentId, row.AttemptCount, row.LeaseOwner, row.LeaseUntilUtc);
    }

    public async Task CompleteAsync(Guid jobId, string workerId, string documentStatus, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC ai.usp_CompleteAiIngestionJob @JobId={jobId}, @WorkerId={workerId}, @DocumentStatus={documentStatus}", cancellationToken);
    }

    public async Task FailAsync(Guid jobId, string workerId, string failureCode, bool retryable, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC ai.usp_FailAiIngestionJob @JobId={jobId}, @WorkerId={workerId}, @FailureCode={failureCode}, @Retryable={retryable}", cancellationToken);
    }

    private sealed class ClaimedJob
    {
        public Guid JobId { get; set; }
        public Guid DocumentId { get; set; }
        public int AttemptCount { get; set; }
        public string LeaseOwner { get; set; } = "";
        public DateTimeOffset LeaseUntilUtc { get; set; }
    }
}
