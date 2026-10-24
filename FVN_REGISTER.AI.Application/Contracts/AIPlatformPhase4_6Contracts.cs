namespace FVN_REGISTER.AI.Application.Contracts;

public sealed class KnowledgeSearchOptions
{
    public double SemanticWeight { get; set; } = 0.75;
    public double LexicalWeight { get; set; } = 0.25;
    public int CandidateLimit { get; set; } = 50;
    public int MaxCandidateScan { get; set; } = 5000;
    public double MinimumScore { get; set; } = 0.05;
}
public sealed record KnowledgePublicationStatus(Guid DocumentId, string Status, int ActiveChunkCount, int PublicationVersion, DateTimeOffset? PublishedAtUtc, DateTimeOffset? UnpublishedAtUtc);
public interface IKnowledgeLifecycleQueryService { Task<KnowledgePublicationStatus?> GetStatusAsync(string tenantCode, string actorId, Guid documentId, IReadOnlyCollection<string> allowedScopes, CancellationToken cancellationToken = default); }
public sealed record GoldenDatasetCase(Guid Id, string TenantCode, string DatasetCode, string CaseType, string InputJson, string ExpectedJson, string? RubricJson, bool IsActive, DateTimeOffset CreatedAtUtc);
public sealed record GoldenDatasetSummary(string DatasetCode, int TotalCases, int ActiveCases, int CompletedCases, int PassedCases, double PassRate, DateTimeOffset? LastEvaluatedAtUtc);
public interface IGoldenDatasetService
{
    Task<Guid> AddCaseAsync(string tenantCode, string actorId, string datasetCode, string caseType, string inputJson, string expectedJson, string? rubricJson, CancellationToken cancellationToken = default);
    Task<GoldenDatasetSummary> GetSummaryAsync(string tenantCode, string datasetCode, CancellationToken cancellationToken = default);
    Task<GoldenDatasetSummary> GetSummaryAsync(string tenantCode, string datasetCode, Guid releaseId, CancellationToken cancellationToken = default);
}
public sealed record AIRelease(Guid Id, string TenantCode, string ReleaseCode, string ModelProvider, string ModelVersion, string PromptVersion, string? SchemaCode, int? SchemaVersion, string GoldenDatasetCode, string Status, double MinimumAcceptanceRate, DateTimeOffset CreatedAtUtc, DateTimeOffset? PromotedAtUtc, DateTimeOffset? RetiredAtUtc);
public sealed record AIReleaseGateResult(bool Passed, double AcceptanceRate, int DecisionCount, int GoldenCases, int GoldenPassed, string[] Failures);
public interface IAIReleaseService
{
    Task<AIRelease> CreateCandidateAsync(string tenantCode, string actorId, string releaseCode, string modelProvider, string modelVersion, string promptVersion, string? schemaCode, int? schemaVersion, string goldenDatasetCode, double minimumAcceptanceRate, CancellationToken cancellationToken = default);
    Task<AIReleaseGateResult> EvaluateGateAsync(string tenantCode, Guid releaseId, CancellationToken cancellationToken = default);
    Task PromoteAsync(string tenantCode, string actorId, Guid releaseId, CancellationToken cancellationToken = default);
    Task RollbackAsync(string tenantCode, string actorId, Guid releaseId, CancellationToken cancellationToken = default);
    Task<AIRelease?> GetActiveAsync(string tenantCode, CancellationToken cancellationToken = default);
}
public sealed record HRCaseAssistantRequest(HRCaseAnalysisRequest Request, OfficialHRFacts Facts);
public interface IHRCaseAssistantAdapter { Task<HRReconciliationAnalysis> AnalyzeAsync(string tenantCode, string actorId, HRCaseAssistantRequest request, CancellationToken cancellationToken = default); }
