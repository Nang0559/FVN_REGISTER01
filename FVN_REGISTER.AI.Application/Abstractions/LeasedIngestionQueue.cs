namespace FVN_REGISTER.AI.Application.Abstractions;

public interface ILeasedIngestionQueue
{
    Task<LeasedIngestionJob?> ClaimAsync(string workerId, int leaseSeconds, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid jobId, string workerId, string documentStatus, CancellationToken cancellationToken = default);
    Task FailAsync(Guid jobId, string workerId, string failureCode, bool retryable, CancellationToken cancellationToken = default);
}
public sealed record LeasedIngestionJob(Guid JobId, Guid DocumentId, int AttemptCount, string LeaseOwner, DateTimeOffset LeaseUntilUtc);
