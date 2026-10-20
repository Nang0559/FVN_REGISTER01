using FVN_REGISTER.AI.Application.Abstractions;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class UnconfiguredAIPlatformAdministration : IAuthorizedAIPlatformAdministration
{
    public Task AuthorizeAsync(string tenantCode, string actorId, string action, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("AI_PLATFORM_ADMIN_AUTHORIZATION_NOT_CONFIGURED");
}
