using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>Implemented by the trusted FVN_REGISTER gateway. Identity must come from the authenticated server-side principal, never model input.</summary>
public interface IAuthorizedFvnBusinessContextProvider
{
    Task<FvnBusinessUserContext> GetContextAsync(string tenantCode, string actorId, CancellationToken cancellationToken = default);
}

/// <summary>Returns caller-visible business vocabulary and allow-listed tool descriptions. Catalog metadata is not executable permission.</summary>
public interface IAuthorizedFvnBusinessToolCatalog
{
    Task<IReadOnlyList<FvnBusinessModuleDescriptor>> GetModulesAsync(FvnBusinessUserContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FvnBusinessToolDescriptor>> GetAvailableToolsAsync(FvnBusinessUserContext context, string? moduleCode = null, CancellationToken cancellationToken = default);
}

/// <summary>The only supported route from AI tool calls into FVN_REGISTER. Host must re-check actor, permission, managed scope and domain policy immediately before execution. No direct database or generic SQL access.</summary>
public interface IAuthorizedFvnBusinessToolExecutor
{
    Task<FvnBusinessToolResult> ExecuteAsync(string tenantCode, string actorId, FvnBusinessToolInvocation invocation, CancellationToken cancellationToken = default);
}
