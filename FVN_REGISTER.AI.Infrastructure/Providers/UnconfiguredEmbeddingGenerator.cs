using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Providers;

public sealed class UnconfiguredEmbeddingGenerator : IEmbeddingGenerator
{
    public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("AI_EMBEDDING_PROVIDER_NOT_CONFIGURED");
}
