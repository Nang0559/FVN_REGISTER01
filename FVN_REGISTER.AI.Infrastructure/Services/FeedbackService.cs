using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Records feedback for evaluation; it never triggers automatic model training.</summary>
public sealed class FeedbackService(AiPlatformDbContext db) : IFeedbackService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
        { "Accept", "Reject", "Correct", "AnswerRating" };

    public async Task RecordAsync(string tenantCode, string reviewerId, Guid? documentId, Guid? extractionId,
        string feedbackType, string? beforeJson, string? afterJson, string? reason,
        CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        reviewerId = Required(reviewerId, 128);
        if (!Allowed.Contains(feedbackType)) throw new ArgumentException("Unsupported feedback type.", nameof(feedbackType));
        ValidateJson(beforeJson, nameof(beforeJson));
        ValidateJson(afterJson, nameof(afterJson));

        if (documentId.HasValue && !await db.Documents.AnyAsync(
            x => x.Id == documentId.Value && x.TenantCode == tenantCode, cancellationToken))
            throw new KeyNotFoundException("Document was not found for this tenant.");
        if (extractionId.HasValue && !await db.Extractions.AnyAsync(
            x => x.Id == extractionId.Value && x.TenantCode == tenantCode, cancellationToken))
            throw new KeyNotFoundException("Extraction was not found for this tenant.");

        db.Feedback.Add(new AiFeedbackRecord
        {
            Id = Guid.NewGuid(), TenantCode = tenantCode, ReviewerId = reviewerId,
            DocumentId = documentId, ExtractionId = extractionId, FeedbackType = feedbackType,
            BeforeJson = beforeJson, AfterJson = afterJson, Reason = reason?.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateJson(string? value, string name)
    {
        if (value is null) return;
        using var _ = System.Text.Json.JsonDocument.Parse(value);
    }
    private static string Required(string value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim() : throw new ArgumentException($"Value must contain 1 to {max} characters.");
}

public sealed class AiFeedbackRecord
{
    public Guid Id { get; set; }
    public string TenantCode { get; set; } = "";
    public Guid? DocumentId { get; set; }
    public Guid? ExtractionId { get; set; }
    public string ReviewerId { get; set; } = "";
    public string FeedbackType { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
