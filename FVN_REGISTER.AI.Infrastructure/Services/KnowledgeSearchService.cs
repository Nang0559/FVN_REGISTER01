using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Permission-aware retrieval orchestration. Scope decisions must come from the trusted FVN gateway.</summary>
public sealed class KnowledgeSearchService(
    IEmbeddingGenerator embeddings,
    IVectorKnowledgeStore store,
    AiPlatformDbContext db) : IKnowledgeSearchService
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
        var vector = await embeddings.GenerateAsync(query, cancellationToken);
        var candidates = await store.SearchAsync(tenantCode, vector, Math.Clamp(limit, 1, 50),
            allowedScopes, cancellationToken);
        if (candidates.Count == 0) return [];

        var documentIds = candidates.Select(x => Guid.Parse(x.Chunk.DocumentId)).Distinct().ToArray();
        var documents = await db.Documents.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode && documentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FileName })
            .ToDictionaryAsync(x => x.Id, x => x.FileName, cancellationToken);

        return candidates.Where(x => documents.ContainsKey(Guid.Parse(x.Chunk.DocumentId)))
            .Select(x =>
            {
                var documentId = Guid.Parse(x.Chunk.DocumentId);
                var pageText = x.Chunk.Metadata.TryGetValue("pageNumber", out var page) && !string.IsNullOrWhiteSpace(page)
                    ? $", page {page}" : "";
                var citation = $"{documents[documentId]}{pageText}, chunk {x.Chunk.Metadata.GetValueOrDefault("chunkOrdinal", "0")}";
                return new KnowledgeSearchHit(x.Chunk.ChunkId, documentId.ToString(), documents[documentId],
                    int.TryParse(page, out var pageNumber) ? pageNumber : null,
                    x.Chunk.Text, x.Score, citation);
            }).ToArray();
    }

    private static string Required(string value, int max, string name) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim() : throw new ArgumentException($"Value must contain 1 to {max} characters.", name);
}
