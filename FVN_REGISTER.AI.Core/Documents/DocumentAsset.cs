namespace FVN_REGISTER.AI.Core.Documents;

public enum DocumentLifecycleStatus { Received, Quarantined, ReadyForProcessing, Processing, AwaitingHumanReview, Approved, Rejected, Failed, Archived }

/// <summary>AI-side metadata only; the original binary is stored through an object-storage adapter.</summary>
public sealed class DocumentAsset
{
    public DocumentAsset(Guid id, string tenantCode, string fileName, string mediaType, long length, string sha256, string uploadedBy)
    {
        if (id == Guid.Empty) throw new ArgumentException("Document id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(tenantCode)) throw new ArgumentException("Tenant code is required.", nameof(tenantCode));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(mediaType)) throw new ArgumentException("Media type is required.", nameof(mediaType));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (string.IsNullOrWhiteSpace(sha256)) throw new ArgumentException("SHA-256 is required.", nameof(sha256));
        if (string.IsNullOrWhiteSpace(uploadedBy)) throw new ArgumentException("Uploader is required.", nameof(uploadedBy));
        Id=id; TenantCode=tenantCode.Trim(); FileName=fileName.Trim(); MediaType=mediaType.Trim();
        Length=length; Sha256=sha256.Trim().ToLowerInvariant(); UploadedBy=uploadedBy.Trim();
        ReceivedAtUtc=DateTimeOffset.UtcNow; Status=DocumentLifecycleStatus.Received;
    }
    public Guid Id { get; }
    public string TenantCode { get; }
    public string FileName { get; }
    public string MediaType { get; }
    public long Length { get; }
    public string Sha256 { get; }
    public string UploadedBy { get; }
    public DateTimeOffset ReceivedAtUtc { get; }
    public DocumentLifecycleStatus Status { get; private set; }
    public void ChangeStatus(DocumentLifecycleStatus next)
    {
        if (Status is DocumentLifecycleStatus.Approved or DocumentLifecycleStatus.Rejected or DocumentLifecycleStatus.Archived)
            throw new InvalidOperationException($"A document in state {Status} is terminal.");
        Status=next;
    }
}