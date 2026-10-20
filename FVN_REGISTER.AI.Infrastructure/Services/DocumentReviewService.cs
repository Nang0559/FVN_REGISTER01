using System.Text.Json;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class DocumentReviewService(AiPlatformDbContext db, FVN_REGISTER.AI.Application.Abstractions.IAuthorizedDocumentReviewProvider authorization) : FVN_REGISTER.AI.Application.Abstractions.IDocumentReviewService
{
    public async Task<FVN_REGISTER.AI.Application.Abstractions.ReviewReceipt> ReviewAsync(Guid extractionId, string tenantCode, string reviewerId, string decision, string? correctedPayloadJson, string? comment, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64, nameof(tenantCode));
        reviewerId = Required(reviewerId, 128, nameof(reviewerId));
        decision = Required(decision, 16, nameof(decision));
        if (decision is not ("Approved" or "Rejected" or "Corrected")) throw new ArgumentException("Decision must be Approved, Rejected or Corrected.", nameof(decision));
        if (decision == "Corrected")
        {
            if (string.IsNullOrWhiteSpace(correctedPayloadJson)) throw new ArgumentException("Corrected JSON is required for a corrected review.", nameof(correctedPayloadJson));
            using var parsed = JsonDocument.Parse(correctedPayloadJson);
        }
        var extraction = await db.Extractions.SingleOrDefaultAsync(x => x.Id == extractionId && x.TenantCode == tenantCode, cancellationToken) ?? throw new KeyNotFoundException("Extraction was not found for this tenant.");
        var document = await db.Documents.SingleAsync(x => x.Id == extraction.DocumentId && x.TenantCode == tenantCode, cancellationToken);
        if (document.Status is "Archived" or "Rejected") throw new InvalidOperationException("DOCUMENT_NOT_REVIEWABLE");
        await authorization.AuthorizeReviewAsync(tenantCode, reviewerId, extractionId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var review = new AiReviewRecord { Id = Guid.NewGuid(), ExtractionId = extraction.Id, DocumentId = extraction.DocumentId, TenantCode = tenantCode, ReviewerId = reviewerId, Decision = decision, OriginalPayloadJson = extraction.CandidateJson ?? "{}", CorrectedPayloadJson = decision == "Corrected" ? correctedPayloadJson : null, Comment = comment?.Trim(), ReviewedAtUtc = now };
        document.Status = decision == "Rejected" ? "Rejected" : "Approved";
        if (decision == "Rejected") document.DedupActive = false;
        if (decision == "Corrected") extraction.CandidateJson = correctedPayloadJson!;
        db.Reviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);
        return new FVN_REGISTER.AI.Application.Abstractions.ReviewReceipt(review.Id, review.DocumentId, review.Decision, now);
    }
    private static string Required(string value, int max, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.", name) : value.Trim();
}

public sealed class AiExtractionRecord
{
    public Guid Id { get; set; } public Guid DocumentId { get; set; } public string TenantCode { get; set; } = ""; public Guid? ReleaseId { get; set; } public string DocumentType { get; set; } = ""; public decimal ClassificationConfidence { get; set; } public string? SchemaCode { get; set; } public int? SchemaVersion { get; set; } public string ValidationStatus { get; set; } = "NotRun"; public string? CandidateJson { get; set; } public string? RepresentationJson { get; set; } public string? ValidationSummaryJson { get; set; } public string? ProvenanceJson { get; set; } public string ParserVersion { get; set; } = ""; public string? ModelProvider { get; set; } public string? ModelVersion { get; set; } public string? PromptVersion { get; set; } public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class AiReviewRecord
{
    public Guid Id { get; set; } public Guid ExtractionId { get; set; } public Guid DocumentId { get; set; } public string TenantCode { get; set; } = ""; public string ReviewerId { get; set; } = ""; public string Decision { get; set; } = ""; public string OriginalPayloadJson { get; set; } = "{}"; public string? CorrectedPayloadJson { get; set; } public string? Comment { get; set; } public DateTimeOffset ReviewedAtUtc { get; set; }
}
