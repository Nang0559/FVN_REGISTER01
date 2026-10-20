using FVN_REGISTER.AI.Application.Abstractions;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Safe default: schema mutation is disabled until FVN_REGISTER verifies administrator rights.</summary>
public sealed class UnconfiguredSchemaManagementProvider : IAuthorizedSchemaManagementProvider
{
    public Task AuthorizeDraftAsync(string tenantCode, string actorId, string moduleCode, string schemaCode,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("SCHEMA_ADMIN_AUTHORIZATION_NOT_CONFIGURED");

    public Task AuthorizePublishAsync(string tenantCode, string actorId, string moduleCode, string schemaCode,
        int version, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("SCHEMA_ADMIN_AUTHORIZATION_NOT_CONFIGURED");
}
