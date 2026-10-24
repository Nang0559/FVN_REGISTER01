using FVN_REGISTER.AI.Application.Abstractions;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Document review decisions require the same FVN right as company-wide publication
/// (function 3502 with data scope "All"). The actor comes from the HMAC-validated gateway header.
/// Fails closed: no right, unknown user or unreadable security tables never authorize a review.
/// </summary>
public sealed class FvnDocumentReviewAuthorization(IAuthorizedKnowledgeAccessProvider knowledgeAccess) : IAuthorizedDocumentReviewProvider
{
    public async Task AuthorizeReviewAsync(string tenantCode, string actorId, Guid extractionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<string> scopes;
        try
        {
            scopes = await knowledgeAccess.GetPublicationScopesAsync(tenantCode, actorId, Guid.Empty, Guid.Empty, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("DOCUMENT_REVIEW_AUTHORIZATION_NOT_CONFIGURED");
        }
        if (scopes.Count == 0) throw new UnauthorizedAccessException();
    }
}
