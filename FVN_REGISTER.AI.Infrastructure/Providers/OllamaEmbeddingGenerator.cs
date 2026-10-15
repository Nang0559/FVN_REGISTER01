using System.Net.Http.Json;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Infrastructure.Providers;

/// <summary>Ollama embedding adapter; kept separate from chat generation for capability-specific routing.</summary>
public sealed class OllamaEmbeddingGenerator(HttpClient client, IOptions<AIPlatformOptions> options) : IEmbeddingGenerator
{
    private readonly AIPlatformOptions settings = options.Value;

    public async Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.EmbeddingModel))
            throw new InvalidOperationException("OLLAMA_EMBEDDING_MODEL_NOT_CONFIGURED");
        if (string.IsNullOrWhiteSpace(text) || text.Length > 24000)
            throw new ArgumentException("Embedding input must contain 1 to 24000 characters.", nameof(text));

        using var response = await client.PostAsJsonAsync("api/embed",
            new EmbedRequest(settings.EmbeddingModel, text), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("embeddings", out var embeddings) ||
            embeddings.ValueKind != JsonValueKind.Array || embeddings.GetArrayLength() != 1)
            throw new InvalidDataException("OLLAMA_EMBEDDING_RESPONSE_INVALID");
        var values = embeddings[0].EnumerateArray().Select(x => x.GetSingle()).ToArray();
        if (values.Length < 8 || values.Length > 16384 || values.Any(x => float.IsNaN(x) || float.IsInfinity(x)))
            throw new InvalidDataException("OLLAMA_EMBEDDING_VECTOR_INVALID");
        return new EmbeddingVector(settings.EmbeddingModel, values);
    }

    private sealed record EmbedRequest(string Model, string Input);
}
