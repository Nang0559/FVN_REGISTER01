using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

public interface IAIModelProvider { string ProviderName { get; } Task<AIModelResponse> GenerateAsync(AIModelRequest request, CancellationToken cancellationToken=default); }
public interface IDocumentTextExtractor { Task<string> ExtractTextAsync(Stream content, string mediaType, CancellationToken cancellationToken=default); }
public interface IDocumentClassifier { Task<(string DocumentType,double Confidence)> ClassifyAsync(string text, CancellationToken cancellationToken=default); }
public interface IDocumentStructuredExtractor { Task<DocumentExtractionResult> ExtractAsync(string text,string documentType,CancellationToken cancellationToken=default); }
public interface IEmbeddingGenerator { Task<EmbeddingVector> GenerateAsync(string text,CancellationToken cancellationToken=default); }
public interface IVectorKnowledgeStore { Task UpsertAsync(IReadOnlyCollection<KnowledgeChunk> chunks,IReadOnlyCollection<EmbeddingVector> vectors,CancellationToken cancellationToken=default); Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query,int limit,IReadOnlyDictionary<string,string>? filters=null,CancellationToken cancellationToken=default); }
public interface IOriginalDocumentStore { Task<string> SaveAsync(Guid documentId,Stream content,CancellationToken cancellationToken=default); Task<Stream> OpenReadAsync(string storageKey,CancellationToken cancellationToken=default); Task DeleteAsync(string storageKey,CancellationToken cancellationToken=default); }
public interface IIngestionQueue { Task EnqueueAsync(Guid documentId,CancellationToken cancellationToken=default); }
public interface IHumanFeedbackStore { Task RecordAsync(HumanFeedback feedback,CancellationToken cancellationToken=default); }