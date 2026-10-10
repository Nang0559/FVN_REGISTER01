namespace FVN_REGISTER.Core.Constants;

public static class AuthorizationScopePolicy
{
    public static string ResolveEffectiveScope(IEnumerable<string?> scopes)
    {
        var normalized = scopes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Any(x => string.Equals(x, AuthorizationScopeCodes.All, StringComparison.OrdinalIgnoreCase)))
            return AuthorizationScopeCodes.All;
        if (normalized.Any(x => string.Equals(x, AuthorizationScopeCodes.Department, StringComparison.OrdinalIgnoreCase)))
            return AuthorizationScopeCodes.Department;
        if (normalized.Any(x => string.Equals(x, AuthorizationScopeCodes.Employee, StringComparison.OrdinalIgnoreCase)))
            return AuthorizationScopeCodes.Employee;
        if (normalized.Any(x => string.Equals(x, AuthorizationScopeCodes.Own, StringComparison.OrdinalIgnoreCase)))
            return AuthorizationScopeCodes.Own;

        return AuthorizationScopeCodes.None;
    }

    public static bool IsManagementScope(string? scope) =>
        string.Equals(scope, AuthorizationScopeCodes.Department, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scope, AuthorizationScopeCodes.All, StringComparison.OrdinalIgnoreCase);

    public static bool IsPersonalScope(string? scope) =>
        string.Equals(scope, AuthorizationScopeCodes.Own, StringComparison.OrdinalIgnoreCase);

    public static bool CanAccess(
        string scope,
        string? actorEmployeeCode,
        int? actorDeptCode,
        string? targetEmployeeCode,
        int? targetDeptCode)
    {
        return scope switch
        {
            var s when string.Equals(s, AuthorizationScopeCodes.All, StringComparison.OrdinalIgnoreCase) => true,
            var s when string.Equals(s, AuthorizationScopeCodes.Department, StringComparison.OrdinalIgnoreCase) =>
                Same(actorDeptCode, targetDeptCode),
            var s when string.Equals(s, AuthorizationScopeCodes.Own, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, AuthorizationScopeCodes.Employee, StringComparison.OrdinalIgnoreCase) =>
                SameEmployee(actorEmployeeCode, targetEmployeeCode),
            _ => false
        };
    }

    private static bool Same(int? left, int? right) =>
        left.HasValue && right.HasValue && left.Value == right.Value;

    private static bool SameEmployee(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
