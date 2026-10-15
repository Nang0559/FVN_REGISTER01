using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// SQL-backed vector store for moderate corpora. Tenant and ACL filtering happens before
/// any candidate text is returned to the RAG orchestration layer.
/// </summary>
public sealed class SqlVectorKnowledgeStore(AiPlatformDbContext db) : IVectorKnowledgeStore
{
    private const int MaximumCandidates = 5000;

    public async Task UpsertAsync(string tenantCode, IReadOnlyCollection<KnowledgeChunk> chunks,
        IReadOnlyCollection<EmbeddingVector> vectors, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        if (chunks.Count == 0 || chunks.Count != vectors.Count)
            throw new ArgumentException("Chunks and vectors must have the same non-zero count.");
        if (chunks.Count > 1000) throw new ArgumentException("A single knowledge upsert is limited to 1000 chunks.");

        var chunkArray = chunks.ToArray();
        var vectorArray = vectors.ToArray();
        for (var i = 0; i < chunkArray.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = chunkArray[i];
            var vector = vectorArray[i];
            if (!Guid.TryParse(chunk.DocumentId, out var documentId) ||
                !Guid.TryParse(chunk.ChunkId, out var chunkId) ||
                !Guid.TryParse(Get(chunk.Metadata, "reviewId"), out var reviewId))
                throw new ArgumentException("ChunkId, DocumentId and metadata.reviewId must be GUID values.");
            if (string.IsNullOrWhiteSpace(chunk.Text) || chunk.Text.Length > 12000)
                throw new ArgumentException("Knowledge chunk text must contain 1 to 12000 characters.");
            if (vector.Values.Count is < 8 or > 16384 || vector.Values.Any(x => !float.IsFinite(x)))
                throw new ArgumentException("Embedding vector is invalid.");
            var scopeJson = Get(chunk.Metadata, "accessScopes") ?? "[]";
            using (var scopeDoc = JsonDocument.Parse(scopeJson))
            {
                if (scopeDoc.RootElement.ValueKind != JsonValueKind.Array ||
                    scopeDoc.RootElement.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String))
                    throw new ArgumentException("accessScopes must be a JSON array of strings.");
            }
            var trustedReview = await db.Reviews.AsNoTracking().AnyAsync(x =>
                x.Id == reviewId && x.TenantCode == tenantCode && x.DocumentId == documentId &&
                (x.Decision == "Approved" || x.Decision == "Corrected"), cancellationToken);
            if (!trustedReview)
                throw new UnauthorizedAccessException("Only a human-approved extraction for this tenant and document can enter the knowledge base.");
            var ordinal = int.TryParse(Get(chunk.Metadata, "chunkOrdinal"), out var parsedOrdinal) ? parsedOrdinal : i;
            var existing = await db.KnowledgeChunks.SingleOrDefaultAsync(x =>
                x.TenantCode == tenantCode && x.DocumentId == documentId && x.ChunkOrdinal == ordinal,
                cancellationToken);
            var record = existing ?? new AiKnowledgeChunkRecord
            {
                Id = chunkId, TenantCode = tenantCode, DocumentId = documentId, ReviewId = reviewId,
                ChunkOrdinal = ordinal, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            if (existing is null) db.KnowledgeChunks.Add(record);
            record.ReviewId = reviewId;
            record.PageNumber = int.TryParse(Get(chunk.Metadata, "pageNumber"), out var page) ? page : null;
            record.SectionPath = Get(chunk.Metadata, "sectionPath");
            record.Content = chunk.Text;
            record.ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk.Text))).ToLowerInvariant();
            record.AccessScopeJson = scopeJson;
            record.EmbeddingModel = vector.Model;
            record.EmbeddingJson = JsonSerializer.Serialize(vector.Values);
            record.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSearchCandidate>> SearchAsync(string tenantCode,
        EmbeddingVector queryVector, int limit, IReadOnlyCollection<string> allowedScopes,
        CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        limit = Math.Clamp(limit, 1, 50);
        if (allowedScopes.Count == 0 || queryVector.Values.Count == 0) return [];
        if (queryVector.Values.Any(x => !float.IsFinite(x))) throw new ArgumentException("Query embedding is invalid.");

        var rows = await (from chunk in db.KnowledgeChunks.AsNoTracking()
                          join review in db.Reviews.AsNoTracking() on chunk.ReviewId equals review.Id
                          where chunk.TenantCode == tenantCode && chunk.IsActive &&
                                (review.Decision == "Approved" || review.Decision == "Corrected") &&
                                chunk.EmbeddingJson != null
                          orderby chunk.CreatedAtUtc descending
                          select chunk).Take(MaximumCandidates).ToListAsync(cancellationToken);

        var allowed = new HashSet<string>(allowedScopes.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        var results = new List<KnowledgeSearchCandidate>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasScope(row.AccessScopeJson, allowed)) continue;
            float[]? vector;
            try { vector = JsonSerializer.Deserialize<float[]>(row.EmbeddingJson!); }
            catch (JsonException) { continue; }
            if (vector is null || vector.Length != queryVector.Values.Count ||
                vector.Any(x => !float.IsFinite(x))) continue;
            var score = Cosine(queryVector.Values, vector);
            if (score <= 0) continue;
            var metadata = new Dictionary<string, string>
            {
                ["pageNumber"] = row.PageNumber?.ToString() ?? "",
                ["sectionPath"] = row.SectionPath ?? "",
                ["chunkOrdinal"] = row.ChunkOrdinal.ToString()
            };
            results.Add(new KnowledgeSearchCandidate(
                new KnowledgeChunk(row.Id.ToString(), row.DocumentId.ToString(), row.Content, metadata), score));
        }
        return results.OrderByDescending(x => x.Score).Take(limit).ToArray();
    }

    private static bool HasScope(string json, HashSet<string> allowed)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array &&
                   document.RootElement.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                       x.GetString() is { } scope && allowed.Contains(scope));
        }
        catch (JsonException) { return false; }
    }

    private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }
        return leftNorm <= 0 || rightNorm <= 0 ? 0 : dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string Required(string value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim() : throw new ArgumentException($"Value must contain 1 to {max} characters.");
}
