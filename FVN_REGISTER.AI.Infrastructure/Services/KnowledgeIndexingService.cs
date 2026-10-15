using System.Text;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Human-controlled publication boundary for RAG. Upload or extraction approval alone never
/// publishes content; an authorized caller must explicitly request publication and supply ACL scopes.
/// </summary>
public sealed class KnowledgeIndexingService(
    AiPlatformDbContext db,
    IOriginalDocumentStore documents,
    IDocumentTextExtractor textExtractor,
    IEmbeddingGenerator embeddings,
    IVectorKnowledgeStore vectorStore) : IKnowledgeIndexingService
{
    public async Task<int> PublishApprovedDocumentAsync(string tenantCode, Guid documentId, Guid reviewId,
        IReadOnlyCollection<string> accessScopes, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        if (accessScopes.Count is < 1 or > 50 ||
            accessScopes.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 128))
            throw new ArgumentException("At least one valid access scope is required.", nameof(accessScopes));

        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == documentId && x.TenantCode == tenantCode && x.Status == "Approved", cancellationToken)
            ?? throw new KeyNotFoundException("Approved document was not found for this tenant.");
        var review = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == reviewId && x.DocumentId == documentId && x.TenantCode == tenantCode &&
                 (x.Decision == "Approved" || x.Decision == "Corrected"), cancellationToken)
            ?? throw new UnauthorizedAccessException("A matching human-approved review is required before publication.");

        await using var original = await documents.OpenReadAsync(document.StorageKey, cancellationToken);
        var text = await textExtractor.ExtractTextAsync(original, document.MediaType, cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("KNOWLEDGE_DOCUMENT_HAS_NO_TEXT");
        if (text.Length > 2_000_000) throw new InvalidDataException("KNOWLEDGE_DOCUMENT_TEXT_LIMIT_EXCEEDED");

        var pieces = SplitIntoChunks(text, 8000);
        if (pieces.Count is < 1 or > 1000) throw new InvalidDataException("KNOWLEDGE_CHUNK_COUNT_OUT_OF_RANGE");
        var scopeJson = JsonSerializer.Serialize(accessScopes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var chunks = new List<KnowledgeChunk>(pieces.Count);
        var vectors = new List<EmbeddingVector>(pieces.Count);
        for (var index = 0; index < pieces.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var piece = pieces[index];
            var id = Guid.NewGuid();
            var metadata = new Dictionary<string, string>
            {
                ["reviewId"] = review.Id.ToString(),
                ["chunkOrdinal"] = index.ToString(),
                ["pageNumber"] = piece.PageNumber?.ToString() ?? "",
                ["sectionPath"] = piece.SectionPath ?? "",
                ["accessScopes"] = scopeJson
            };
            chunks.Add(new KnowledgeChunk(id.ToString(), documentId.ToString(), piece.Text, metadata));
            vectors.Add(await embeddings.GenerateAsync(piece.Text, cancellationToken));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var previous = await db.KnowledgeChunks.Where(x =>
            x.TenantCode == tenantCode && x.DocumentId == documentId && x.IsActive).ToListAsync(cancellationToken);
        foreach (var item in previous) item.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        await vectorStore.UpsertAsync(tenantCode, chunks, vectors, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return chunks.Count;
    }

    public async Task UnpublishDocumentAsync(string tenantCode, Guid documentId,
        CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        var rows = await db.KnowledgeChunks.Where(x =>
            x.TenantCode == tenantCode && x.DocumentId == documentId && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var row in rows) row.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<TextChunk> SplitIntoChunks(string text, int maximumCharacters)
    {
        var result = new List<TextChunk>();
        var buffer = new StringBuilder();
        int? page = null;
        var section = (string?)null;
        var currentPage = 1;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("[PAGE ", StringComparison.Ordinal) && line.EndsWith(']') &&
                int.TryParse(line.AsSpan(6, line.Length - 7), out var pageNumber))
            {
                Flush();
                currentPage = pageNumber;
                continue;
            }
            if (line.Length > maximumCharacters)
            {
                Flush();
                for (var offset = 0; offset < line.Length; offset += maximumCharacters)
                    result.Add(new TextChunk(line.Substring(offset, Math.Min(maximumCharacters, line.Length - offset)), currentPage, section));
                continue;
            }
            if (buffer.Length > 0 && buffer.Length + line.Length + 1 > maximumCharacters) Flush();
            if (buffer.Length == 0) page = currentPage;
            if (line.Length <= 180 && (line.EndsWith(':') || line.StartsWith('#')))
                section = line;
            if (buffer.Length > 0) buffer.AppendLine();
            buffer.Append(line);
        }
        Flush();
        return result;

        void Flush()
        {
            if (buffer.Length == 0) return;
            result.Add(new TextChunk(buffer.ToString(), page, section));
            buffer.Clear();
            page = null;
        }
    }

    private static string Required(string value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim() : throw new ArgumentException($"Value must contain 1 to {max} characters.");

    private sealed record TextChunk(string Text, int? PageNumber, string? SectionPath);
}
