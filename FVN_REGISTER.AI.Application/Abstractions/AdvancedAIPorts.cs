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
    Task<ExtractionReviewView?> GetLatestAsync(Guid documentId, string tenantCode, CancellationToken cancellationToken = default);
}
public sealed record ExtractionReviewView(Guid ExtractionId, Guid DocumentId, string DocumentType,
    double ClassificationConfidence, string CandidateJson, string ParserVersion,
    string? ModelProvider, string? ModelVersion, string? PromptVersion, DateTimeOffset CreatedAtUtc);
