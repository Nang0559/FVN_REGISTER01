namespace FVN_REGISTER.Core.Constants;

/// <summary>
/// Canonical authorization rule for designated feature operators.
/// Assignment never replaces RBAC; both capability and assignment are required.
/// </summary>
public static class FeatureOperatorAuthorizationPolicy
{
    public static bool CanOperate(bool hasRbacCapability, bool isAssignedOperator)
        => hasRbacCapability && isAssignedOperator;

    /// <summary>
    /// Operator selection gate: an assigned operator must retain the RBAC
    /// capability and have Managed Scope access to the current target employee.
    /// </summary>
    public static bool CanSelectOperator(
        bool hasRbacCapability,
        bool hasTargetScopeAccess)
        => hasRbacCapability && hasTargetScopeAccess;

    /// <summary>
    /// Assignment creation gate: employee, active user account and effective RBAC
    /// capability are all required before a Feature Operator assignment can be stored.
    /// </summary>
    public static bool CanAssignOperator(
        bool employeeExists,
        bool activeUserExists,
        bool hasRbacCapability)
        => employeeExists && activeUserExists && hasRbacCapability;

    /// <summary>
    /// Selects the first currently valid assigned operator. Candidates are already
    /// ordered by assignment creation/ID; an assigned user whose RBAC was revoked
    /// must be skipped rather than selected.
    /// </summary>
    public static (int Id, string EmployeeCode)? SelectFirstEligibleOperator(
        IEnumerable<(int Id, string EmployeeCode, bool HasRbac)> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (CanOperate(candidate.HasRbac, true))
                return (candidate.Id, candidate.EmployeeCode);
        }

        return null;
    }
}
