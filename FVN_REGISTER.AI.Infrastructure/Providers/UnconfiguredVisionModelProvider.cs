using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Providers;

public sealed class UnconfiguredVisionModelProvider : IAIVisionModelProvider
{
    public string ProviderName => "Unconfigured";
    public string ModelVersion => "Unconfigured";
    public Task<AIModelResponse> AnalyzeImageAsync(Stream image, string mediaType, AIModelRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("VISION_MODEL_NOT_CONFIGURED");
}
