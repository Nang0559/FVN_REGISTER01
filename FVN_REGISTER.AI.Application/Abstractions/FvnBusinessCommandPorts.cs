using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>
/// The only AI-side boundary for state-changing FVN operations. The host must re-authorize
/// the current actor and managed scope during both preview and execution, validate domain
/// policy, bind preview to a canonical command hash, enforce expiry/single-use/idempotency,
/// and write an immutable audit record. The model must never call ExecuteConfirmedAsync.
/// </summary>
public interface IAuthorizedFvnBusinessCommandGateway
{
    Task<FvnBusinessCommandPreview> PreviewAsync(
        string tenantCode,
        string actorId,
        FvnBusinessCommandPreviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called only after a human explicitly confirms the exact preview in the trusted FVN UI.
    /// The host must reject changed/expired/reused previews and deduplicate by idempotency key.
    /// </summary>
    Task<FvnBusinessCommandExecutionResult> ExecuteConfirmedAsync(
        string tenantCode,
        string actorId,
        FvnBusinessCommandConfirmation confirmation,
        CancellationToken cancellationToken = default);
}
