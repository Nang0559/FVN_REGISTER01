namespace FVN_REGISTER.Contract.Dtos.Security;

public sealed class PermissionSnapshotDto
{
    public int UserId { get; set; }
    public int? PermissionCode { get; set; }
    public List<int> RoleCodes { get; set; } = new();
    public List<SecurityFunctionDto> Functions { get; set; } = new();
    public HashSet<int> FunctionCodes { get; set; } = new();

    public bool Has(int functionCode) => FunctionCodes.Contains(functionCode);

    public bool HasScope(int functionCode, string scope) =>
        Functions.Any(x => x.FunctionCode == functionCode &&
            string.Equals(x.ScopeCode, scope, StringComparison.OrdinalIgnoreCase));

    public bool HasPersonal(int functionCode) => HasScope(functionCode, FVN_REGISTER.Core.Constants.AuthorizationScopeCodes.Own);

    public bool HasManagement(int functionCode) =>
        Functions.Any(x => x.FunctionCode == functionCode &&
            FVN_REGISTER.Core.Constants.AuthorizationScopePolicy.IsManagementScope(x.ScopeCode));
}

public sealed class SecurityFunctionDto
{
    public int IdFunction { get; set; }
    public int FunctionCode { get; set; }
    public string FunctionName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? ModuleCode { get; set; }
    public string? ActionCode { get; set; }
    public string? ScopeCode { get; set; }
    public int DisplayOrder { get; set; }
}

