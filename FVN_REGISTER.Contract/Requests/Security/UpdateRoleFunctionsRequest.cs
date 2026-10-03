namespace FVN_REGISTER.Contract.Requests.Security;

public sealed class UpdateRoleFunctionsRequest
{
    public int RoleCode { get; set; }
    public List<int> FunctionCodes { get; set; } = new();

    /// <summary>FunctionCode -> scope override for this role. Null/absent uses the function default scope.</summary>
    public Dictionary<int, string?> ScopeOverrides { get; set; } = new();

    /// <summary>FunctionCode -> Personal/Management capability mode for this role.</summary>
    public Dictionary<int, string?> AccessModeOverrides { get; set; } = new();
}
