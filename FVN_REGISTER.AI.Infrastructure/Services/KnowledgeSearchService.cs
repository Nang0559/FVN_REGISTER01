using System.Text.RegularExpressions;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Permission-aware hybrid retrieval: vector candidates are ACL-filtered first, then reranked by semantic and lexical relevance.</summary>
public sealed class KnowledgeSearchService(
    IEmbeddingGenerator embeddings,
    IVectorKnowledgeStore store,
    AiPlatformDbContext db,
    IOptions<KnowledgeSearchOptions> options) : IKnowledgeSearchService
{
    public async Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(string tenantCode, string actorId,
        IReadOnlyCollection<string> allowedScopes, string query, int limit,
        CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64, nameof(tenantCode));
        actorId = Required(actorId, 128, nameof(actorId));
        ArgumentNullException.ThrowIfNull(allowedScopes);
        if (string.IsNullOrWhiteSpace(query) || query.Length > 4000)
            throw new ArgumentException("Search query must contain 1 to 4000 characters.", nameof(query));
        if (allowedScopes.Count == 0) return [];
        var settings = options.Value;
        var semanticWeight = Math.Clamp(settings.SemanticWeight, 0, 1);
        var lexicalWeight = Math.Clamp(settings.LexicalWeight, 0, 1);
        var totalWeight = semanticWeight + lexicalWeight;
        if (totalWeight <= 0) return [];
        semanticWeight /= totalWeight; lexicalWeight /= totalWeight;
        var vector = await embeddings.GenerateAsync(query, cancellationToken);
        var candidates = await store.SearchAsync(tenantCode, vector,
            Math.Clamp(Math.Max(limit, settings.CandidateLimit), 1, 50), allowedScopes, cancellationToken);
        if (candidates.Count == 0) return [];

        var documentIds = candidates.Select(x => Guid.Parse(x.Chunk.DocumentId)).Distinct().ToArray();
        var documents = await db.Documents.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode && documentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FileName })
            .ToDictionaryAsync(x => x.Id, x => x.FileName, cancellationToken);
        var queryTokens = Tokens(query);
        // MinimumScore is a cosine threshold. Apply it to the raw vector similarity
        // before mapping cosine from [-1,1] into the blended ranking score [0,1].
        var minScore = Math.Clamp(settings.MinimumScore, -1, 1);

        return candidates
            .Where(x => x.Score >= minScore)
            .Where(x => documents.ContainsKey(Guid.Parse(x.Chunk.DocumentId)))
            .Select(x =>
            {
                var documentId = Guid.Parse(x.Chunk.DocumentId);
                var lexical = LexicalScore(queryTokens, x.Chunk.Text);
                var score = semanticWeight * Math.Clamp((x.Score + 1) / 2, 0, 1) + lexicalWeight * lexical;
                var page = x.Chunk.Metadata.GetValueOrDefault("pageNumber", "");
                var pageText = string.IsNullOrWhiteSpace(page) ? "" : $", page {page}";
                var citation = $"{documents[documentId]}{pageText}, chunk {x.Chunk.Metadata.GetValueOrDefault("chunkOrdinal", "0")}";
                return new { Hit = new KnowledgeSearchHit(x.Chunk.ChunkId, documentId.ToString(), documents[documentId], int.TryParse(page, out var pageNumber) ? pageNumber : null, x.Chunk.Text, score, citation), Score = score };
            })
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 20))
            .Select(x => x.Hit)
            .ToArray();
    }

    private static string[] Tokens(string text) => Regex.Matches(text.ToLowerInvariant(), "[\\p{L}\\p{N}]{2,}")
        .Select(x => x.Value).Distinct(StringComparer.Ordinal).Take(256).ToArray();

    private static double LexicalScore(IReadOnlyCollection<string> queryTokens, string text)
    {
        if (queryTokens.Count == 0 || string.IsNullOrWhiteSpace(text)) return 0;
        var haystack = Tokens(text).ToHashSet(StringComparer.Ordinal);
        var overlap = queryTokens.Count(token => haystack.Contains(token));
        var score = (double)overlap / queryTokens.Count;
        if (queryTokens.Count >= 2 && text.Contains(string.Join(' ', queryTokens), StringComparison.OrdinalIgnoreCase)) score = Math.Min(1, score + 0.25);
        return score;
    }

    private static string Required(string value, int max, string name) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max ? value.Trim() : throw new ArgumentException($"Value must contain 1 to {max} characters.", name);
}
