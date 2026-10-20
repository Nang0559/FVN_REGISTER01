using FVN_REGISTER.AI.Application.Abstractions;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Safe default: knowledge access is unavailable until FVN_REGISTER supplies a trusted
/// authorization adapter. Client-provided access scopes are never accepted as authority.
/// </summary>
public sealed class UnconfiguredAuthorizedKnowledgeAccessProvider : IAuthorizedKnowledgeAccessProvider
{
    private static InvalidOperationException NotConfigured() =>
        new("KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED");

    public Task<IReadOnlyCollection<string>> GetSearchScopesAsync(
        string tenantCode, string actorId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<IReadOnlyCollection<string>> GetPublicationScopesAsync(
        string tenantCode, string actorId, Guid documentId, Guid reviewId,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task AuthorizeUnpublishAsync(
        string tenantCode, string actorId, Guid documentId,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();
}
