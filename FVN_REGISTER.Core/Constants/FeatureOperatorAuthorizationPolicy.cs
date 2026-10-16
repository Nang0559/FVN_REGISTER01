namespace FVN_REGISTER.Core.Constants;

/// <summary>
/// Canonical authorization rule for designated feature operators.
///
/// Default (legacy) rule: assignment never replaces RBAC; both capability and assignment are required.
/// Capability-granting rule (<see cref="FeatureOperatorCatalog.AssignmentGrantsCapability"/>, e.g.
/// Execution.Review): effective permission = role grant OR active assignment. The assignment
/// itself creates the capability, so the role does not need to be ticked.
/// </summary>
public static class FeatureOperatorAuthorizationPolicy
{
    public static bool CanOperate(bool hasRbacCapability, bool isAssignedOperator)
        => hasRbacCapability && isAssignedOperator;

    /// <summary>
    /// Function-aware operate rule. For capability-granting functions
    /// <paramref name="hasEffectiveCapability"/> already includes the assignment (resolved centrally by
    /// AuthorizationService), so no additional "assigned" requirement applies. For every other function
    /// the legacy RBAC-and-assignment rule is kept.
    /// </summary>
    public static bool CanOperate(int functionCode, bool hasEffectiveCapability, bool isAssignedOperator)
        => FeatureOperatorCatalog.AssignmentGrantsCapability(functionCode)
            ? hasEffectiveCapability
            : CanOperate(hasEffectiveCapability, isAssignedOperator);

    /// <summary>
    /// Function-aware assignment creation gate. Capability-granting functions do not require the
    /// target to already hold the role grant; every other function does.
    /// </summary>
    public static bool CanAssignOperator(
        int functionCode,
        bool employeeExists,
        bool activeUserExists,
        bool hasRbacCapability)
        => employeeExists
           && activeUserExists
           && (FeatureOperatorCatalog.AssignmentGrantsCapability(functionCode) || hasRbacCapability);

    /// <summary>
    /// An assigner may only grant what they hold themselves (SuperAdmin excepted).
    /// </summary>
    public static bool CanGrantAssignment(bool assignerIsSuperAdmin, bool assignerHasCapability)
        => assignerIsSuperAdmin || assignerHasCapability;

    /// <summary>Scope ordering used to prevent granting a broader scope than the assigner holds.</summary>
    public static int ScopeRank(string? scope) => scope?.Trim().ToLowerInvariant() switch
    {
        "all" => 4,
        "department" => 3,
        "employee" => 2,
        "own" => 1,
        _ => 0
    };

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
