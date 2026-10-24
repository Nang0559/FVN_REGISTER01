using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Reuses the permission-aware knowledge search of the document RAG. Access scopes are resolved freshly for the signed tenant and actor
/// (the host snapshot is never an ACL grant) and the search itself applies the configured minimum similarity, so unrelated documents are not offered.
/// </summary>
public sealed class GuideDocumentEvidenceProvider(
    IKnowledgeSearchService search,
    ILogger<GuideDocumentEvidenceProvider> logger,
    IAuthorizedKnowledgeAccessProvider? access = null) : IGuideDocumentEvidenceProvider
{
    private const int MaxPassages = 3;
    private const int MaxPassageChars = 900;

    public async Task<IReadOnlyList<GuideDocumentEvidence>> FindAsync(string tenantCode, string actorId, string query, CancellationToken cancellationToken = default)
    {
        if (access is null || string.IsNullOrWhiteSpace(query)) return [];
        try
        {
            var scopes = await access.GetSearchScopesAsync(tenantCode, actorId, cancellationToken);
            if (scopes.Count == 0) return [];
            var hits = await search.SearchAsync(tenantCode, actorId, scopes, query.Trim(), MaxPassages * 2, cancellationToken);
            var result = new List<GuideDocumentEvidence>();
            foreach (var hit in hits)
            {
                if (result.Count >= MaxPassages) break;
                var text = hit.Text?.Trim() ?? string.Empty;
                if (text.Length == 0) continue;
                if (text.Length > MaxPassageChars) text = text[..MaxPassageChars];
                var id = "D" + (result.Count + 1);
                var title = hit.Citation.Length > 280 ? hit.Citation[..280] : hit.Citation;
                result.Add(new GuideDocumentEvidence(
                    new GuideEvidence(id, title, text),
                    new GuideDocumentSource(id, title, hit.DocumentId, hit.FileName, hit.PageNumber)));
            }
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Documents are an extra source: a search or authorization problem must not take the user guide answer down with it.
            logger.LogWarning(ex, "Guide assistant could not retrieve approved documents.");
            return [];
        }
    }
}
