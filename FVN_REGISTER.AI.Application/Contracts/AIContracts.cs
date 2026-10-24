namespace FVN_REGISTER.AI.Application.Contracts;

public sealed record AIModelRequest(string Task, string Input, string? SystemInstruction=null, string? ModelHint=null, IReadOnlyDictionary<string,string>? Metadata=null);
public sealed record AIModelResponse(string Output, string Provider, string Model, string? FinishReason, int? InputTokens, int? OutputTokens, TimeSpan Duration);
public sealed record ExtractedField(string Name, string? Value, double Confidence, string? SourcePage, string? SourceRegion);
public sealed record DocumentExtractionResult(string DocumentType, double ClassificationConfidence, IReadOnlyList<ExtractedField> Fields, string ParserVersion, string? ModelVersion);
public sealed record EmbeddingVector(string Model, IReadOnlyList<float> Values);
public sealed record KnowledgeChunk(string ChunkId, string DocumentId, string Text, IReadOnlyDictionary<string,string> Metadata);
public sealed record HumanFeedback(Guid Id, Guid DocumentId, string ReviewerId, string Decision, string? CorrectedPayload, string? Comment, DateTimeOffset SubmittedAtUtc);
public sealed record ReviewReceipt(Guid ReviewId, Guid DocumentId, string Decision, DateTimeOffset ReviewedAtUtc);