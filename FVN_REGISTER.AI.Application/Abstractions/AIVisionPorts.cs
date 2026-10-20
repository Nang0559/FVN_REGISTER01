using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>Optional multimodal model contract for handwritten/image documents. It is deliberately separate from text generation.</summary>
public interface IAIVisionModelProvider
{
    string ProviderName { get; }
    string ModelVersion { get; }
    Task<AIModelResponse> AnalyzeImageAsync(Stream image, string mediaType, AIModelRequest request, CancellationToken cancellationToken = default);
}
