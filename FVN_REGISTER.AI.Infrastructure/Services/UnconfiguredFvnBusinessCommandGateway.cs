using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Default-deny command gateway. No transaction command is executable until a trusted FVN adapter is installed.</summary>
public sealed class UnconfiguredFvnBusinessCommandGateway : IAuthorizedFvnBusinessCommandGateway
{
    private static InvalidOperationException NotConfigured() =>
        new("FVN_BUSINESS_COMMAND_GATEWAY_NOT_CONFIGURED");

    public Task<FvnBusinessCommandPreview> PreviewAsync(
        string tenantCode, string actorId, FvnBusinessCommandPreviewRequest request,
        CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<FvnBusinessCommandExecutionResult> ExecuteConfirmedAsync(
        string tenantCode, string actorId, FvnBusinessCommandConfirmation confirmation,
        CancellationToken cancellationToken = default) => throw NotConfigured();
}
