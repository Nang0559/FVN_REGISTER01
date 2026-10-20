using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Safe default for deployments that have not installed the trusted FVN_REGISTER host adapter.
/// The AI service must not infer identity, permissions, scopes, or executable tools from prompts
/// or unsigned client payloads.
/// </summary>
public sealed class UnconfiguredFvnBusinessGateway :
    IAuthorizedFvnBusinessContextProvider,
    IAuthorizedFvnBusinessToolCatalog,
    IAuthorizedFvnBusinessToolExecutor
{
    private static InvalidOperationException NotConfigured() =>
        new("FVN_BUSINESS_GATEWAY_NOT_CONFIGURED");

    public Task<FvnBusinessUserContext> GetContextAsync(
        string tenantCode, string actorId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<IReadOnlyList<FvnBusinessModuleDescriptor>> GetModulesAsync(
        FvnBusinessUserContext context, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<IReadOnlyList<FvnBusinessToolDescriptor>> GetAvailableToolsAsync(
        FvnBusinessUserContext context, string? moduleCode = null,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<FvnBusinessToolResult> ExecuteAsync(
        string tenantCode, string actorId, FvnBusinessToolInvocation invocation,
        CancellationToken cancellationToken = default) =>
        throw NotConfigured();
}
