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
    /// Assignment creation gate: employee, active user account and effective RBAC
    /// capability are all required before a Feature Operator assignment can be stored.
    /// </summary>
    public static bool CanAssignOperator(
        bool employeeExists,
        bool activeUserExists,
        bool hasRbacCapability)
        => employeeExists && activeUserExists && hasRbacCapability;
}
