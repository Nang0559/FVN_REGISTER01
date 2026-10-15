using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Application.Contracts;
namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>Scanner adapter must return Clean only after a positive engine verdict.</summary>
public interface IMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}
public sealed record MalwareScanResult(MalwareScanVerdict Verdict, string Engine, string? SignatureVersion, string? FailureCode);
public enum MalwareScanVerdict { Clean, Infected, Unknown }

public interface IDocumentPipeline
{
    Task ProcessAsync(Guid documentId, string workerId, CancellationToken cancellationToken = default);
}

public interface IDocumentReviewService
{
    Task<ReviewReceipt> ReviewAsync(Guid extractionId, string tenantCode, string reviewerId,
        string decision, string? correctedPayloadJson, string? comment, CancellationToken cancellationToken = default);
}
public sealed record ReviewReceipt(Guid ReviewId, Guid DocumentId, string Decision, DateTimeOffset ReviewedAtUtc);

public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(string tenantCode, string actorId,
        IReadOnlyCollection<string> allowedScopes, string query, int limit, CancellationToken cancellationToken = default);
}
public sealed record KnowledgeSearchHit(string DocumentId, string FileName, int? PageNumber,
    string Text, double Score, string Citation);

public interface IFeedbackService
{
    Task RecordAsync(string tenantCode, string reviewerId, Guid? documentId, Guid? extractionId,
        string feedbackType, string? beforeJson, string? afterJson, string? reason,
        CancellationToken cancellationToken = default);
}

public interface IDocumentExtractionQueryService
{
    Task<ExtractionReviewView?> GetLatestAsync(Guid documentId, string tenantCode, string actorId, CancellationToken cancellationToken = default);
}
public sealed record ExtractionReviewView(Guid ExtractionId, Guid DocumentId, string DocumentType,
    double ClassificationConfidence, string CandidateJson, string ParserVersion,
    string? ModelProvider, string? ModelVersion, string? PromptVersion, DateTimeOffset CreatedAtUtc);

/// <summary>Validates generic extraction candidates against a versioned module-owned schema.</summary>
public interface IExtractionValidationService
{
    Task<ExtractionValidationResult> ValidateAsync(ExtractionValidationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Extracts fields according to a caller-supplied versioned schema, without module-specific assumptions.</summary>
public interface ISchemaDrivenDocumentExtractor
{
    Task<IReadOnlyList<ExtractedCandidate>> ExtractAsync(string text, ExtractionSchema schema,
        CancellationToken cancellationToken = default);
}

/// <summary>Explicitly publishes a human-approved document into the tenant-scoped knowledge index.</summary>
public interface IKnowledgeIndexingService
{
    Task<int> PublishApprovedDocumentAsync(string tenantCode, Guid documentId, Guid reviewId,
        IReadOnlyCollection<string> accessScopes, CancellationToken cancellationToken = default);
    Task UnpublishDocumentAsync(string tenantCode, Guid documentId, CancellationToken cancellationToken = default);
}
