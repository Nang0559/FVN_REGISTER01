using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>Reusable document-to-structured-data workflow. Business modules provide schemas/templates; AI never writes operational data directly.</summary>
public interface IDocumentDataEntryService
{
    Task<DataEntryRunReceipt> CreateRunAsync(string tenantCode, string actorId, string fileName, Stream documentContent,
        string templateCode, CancellationToken cancellationToken = default);
    Task<DataEntryRunView?> GetRunAsync(Guid runId, string tenantCode, string actorId, CancellationToken cancellationToken = default);
    Task ProcessAsync(Guid runId, string workerId, CancellationToken cancellationToken = default);
    Task<DataEntryRunView> ConfirmAsync(Guid runId, string tenantCode, string actorId, string confirmedRowsJson, string? comment = null,
        CancellationToken cancellationToken = default);
    Task<StoredDataEntryFile?> ExportAsync(Guid runId, string tenantCode, string actorId, CancellationToken cancellationToken = default);
}

public interface IDocumentDataEntryTemplateService
{
    Task<DataEntryTemplateView> CreateAsync(string tenantCode, string actorId, string templateCode, string displayName,
        string moduleCode, string schemaCode, Stream templateContent, DataEntryTemplateDefinition definition,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataEntryTemplateView>> ListAsync(string tenantCode, CancellationToken cancellationToken = default);
}

/// <summary>Optional multimodal provider. Implementations may inspect the original image directly; OCR remains the provider-neutral fallback.</summary>
public interface IAIVisionModelProvider
{
    string ProviderName { get; }
    string ModelVersion { get; }
    Task<AIModelResponse> AnalyzeImageAsync(Stream image, string mediaType, AIModelRequest request, CancellationToken cancellationToken = default);
}
