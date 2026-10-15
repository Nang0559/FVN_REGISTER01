using FVN_REGISTER.AI.Application.Contracts;
namespace FVN_REGISTER.AI.Application.Abstractions;
public interface IAIModelProvider { string ProviderName { get; } Task<AIModelResponse> GenerateAsync(AIModelRequest request,CancellationToken cancellationToken=default); }
public interface IDocumentTextExtractor { Task<string> ExtractTextAsync(Stream content,string mediaType,CancellationToken cancellationToken=default); }
public interface IOcrTextExtractor { Task<string> ExtractTextAsync(Stream content,string mediaType,CancellationToken cancellationToken=default); }
public interface IDocumentClassifier { Task<(string DocumentType,double Confidence)> ClassifyAsync(string text,CancellationToken cancellationToken=default); }
public interface IDocumentStructuredExtractor { Task<DocumentExtractionResult> ExtractAsync(string text,string documentType,CancellationToken cancellationToken=default); }
public interface IEmbeddingGenerator { Task<EmbeddingVector> GenerateAsync(string text,CancellationToken cancellationToken=default); }
public interface IVectorKnowledgeStore
{
 Task UpsertAsync(string tenantCode,IReadOnlyCollection<KnowledgeChunk> chunks,IReadOnlyCollection<EmbeddingVector> vectors,CancellationToken cancellationToken=default);
 Task<IReadOnlyList<KnowledgeSearchCandidate>> SearchAsync(string tenantCode,EmbeddingVector queryVector,int limit,IReadOnlyCollection<string> allowedScopes,CancellationToken cancellationToken=default);
}
public sealed record KnowledgeSearchCandidate(KnowledgeChunk Chunk,double Score);
public interface IOriginalDocumentStore { Task<StoredDocument> SaveAsync(Guid documentId,Stream content,CancellationToken cancellationToken=default); Task<Stream> OpenReadAsync(string storageKey,CancellationToken cancellationToken=default); Task DeleteAsync(string storageKey,CancellationToken cancellationToken=default); }
public sealed record StoredDocument(string StorageKey,long Length,string Sha256);
public interface IUploadContentValidator { Task<ValidatedUpload> ValidateAsync(Stream content,string fileName,CancellationToken cancellationToken=default); }
public sealed record ValidatedUpload(string FileName,string MediaType,string Extension);
public interface IDocumentIngestionService { Task<IngestionReceipt> IngestAsync(string tenantCode,string actorId,string fileName,Stream content,CancellationToken cancellationToken=default); Task<DocumentStatusResult?> GetStatusAsync(Guid documentId,string tenantCode,CancellationToken cancellationToken=default); }
public sealed record IngestionReceipt(Guid DocumentId,Guid JobId,string FileName,long Length,string Sha256,string Status,bool Duplicate);
public sealed record DocumentStatusResult(Guid DocumentId,string FileName,string Status,DateTimeOffset ReceivedAtUtc,Guid? JobId,string? JobStatus,string? FailureCode);
public interface IIngestionQueue { Task EnqueueAsync(Guid documentId,CancellationToken cancellationToken=default); }
public interface IHumanFeedbackStore { Task RecordAsync(HumanFeedback feedback,CancellationToken cancellationToken=default); }