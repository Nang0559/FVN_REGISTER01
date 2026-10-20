namespace FVN_REGISTER.AI.Application.Contracts;

/// <summary>A short-lived FVN_REGISTER identity and authorization snapshot. It is not an authorization grant; the host re-authorizes every tool invocation.</summary>
public sealed record FvnBusinessUserContext(string TenantCode, string ActorId, string? EmployeeCode, string? DepartmentCode, string? PositionCode, IReadOnlyCollection<string> RoleCodes, IReadOnlyCollection<string> PermissionCodes, IReadOnlyCollection<string> ManagedScopeCodes, DateTimeOffset RetrievedAtUtc, string ContextVersion);

/// <summary>Business vocabulary metadata only; never contains employee or business rows.</summary>
public sealed record FvnBusinessModuleDescriptor(string ModuleCode, string DisplayName, string Description, IReadOnlyCollection<FvnBusinessFunctionDescriptor> Functions);
public sealed record FvnBusinessFunctionDescriptor(string FunctionCode, string DisplayName, string Description, IReadOnlyCollection<FvnBusinessActionDescriptor> Actions);
public sealed record FvnBusinessActionDescriptor(string ActionCode, string DisplayName, string Description, string RequiredPermissionCode, bool IsReadOnly, bool RequiresHumanConfirmation);

/// <summary>A model-visible allow-listed tool contract. InputSchemaJson must not expose SQL, credentials, arbitrary URLs or generic HTTP execution.</summary>
public sealed record FvnBusinessToolDescriptor(string ToolCode, string ModuleCode, string FunctionCode, string ActionCode, string DisplayName, string Description, string InputSchemaJson, string RequiredPermissionCode, IReadOnlyCollection<string> RequiredScopeTypes, bool IsReadOnly, bool RequiresHumanConfirmation, string ToolVersion);
public sealed record FvnBusinessToolInvocation(string ToolCode, string InputJson, string? ConversationId, string? CorrelationId);
public sealed record FvnBusinessToolResult(string ToolCode, string Status, string? ResultJson, IReadOnlyCollection<FvnBusinessProvenance> Provenance, bool RequiresHumanConfirmation, string? FailureCode);
public sealed record FvnBusinessProvenance(string SourceSystem, string SourceType, string SourceReference, DateTimeOffset RetrievedAtUtc, string? Version);
