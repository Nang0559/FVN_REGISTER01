using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class KnowledgeLifecycleQueryService(AiPlatformDbContext db) : IKnowledgeLifecycleQueryService
{
    public async Task<KnowledgePublicationStatus?> GetStatusAsync(string tenantCode, string actorId, Guid documentId,
        IReadOnlyCollection<string> allowedScopes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Tenant and actor are required.");
        if (allowedScopes.Count == 0) return null;
        var documentApproved = await db.Documents.AsNoTracking().AnyAsync(
            x => x.Id == documentId && x.TenantCode == tenantCode.Trim() && x.Status == "Approved",
            cancellationToken);
        if (!documentApproved) return null;

        // An active chunk is not sufficient evidence of a current publication: a reviewer
        // may revoke approval after indexing. Re-check the human review state at read time.
        var approvedReviewIds = db.Reviews.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode.Trim() && x.DocumentId == documentId &&
                        (x.Decision == "Approved" || x.Decision == "Corrected"))
            .Select(x => x.Id);
        var rows = await db.KnowledgeChunks.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode.Trim() && x.DocumentId == documentId &&
                        x.IsActive && approvedReviewIds.Contains(x.ReviewId))
            .ToListAsync(cancellationToken);
        var allowed = new HashSet<string>(allowedScopes.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        var visible = rows.Where(x => HasScope(x.AccessScopeJson, allowed)).ToArray();
        if (visible.Length == 0) return new KnowledgePublicationStatus(documentId, "Unpublished", 0, 0, null, DateTimeOffset.UtcNow);
        var latest = visible.Max(x => x.CreatedAtUtc);
        return new KnowledgePublicationStatus(documentId, "Published", visible.Length, 1, latest, null);
    }

    private static bool HasScope(string json, HashSet<string> allowed)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() is { } scope && allowed.Contains(scope));
        }
        catch (JsonException) { return false; }
    }
}
