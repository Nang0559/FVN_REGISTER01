using FVN_REGISTER.AI.Application.Abstractions;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Safe default: review decisions are disabled until reviewer rights are verified by FVN_REGISTER.</summary>
public sealed class UnconfiguredDocumentReviewProvider : IAuthorizedDocumentReviewProvider
{
    public Task AuthorizeReviewAsync(string tenantCode, string actorId, Guid extractionId,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("DOCUMENT_REVIEW_AUTHORIZATION_NOT_CONFIGURED");
}
