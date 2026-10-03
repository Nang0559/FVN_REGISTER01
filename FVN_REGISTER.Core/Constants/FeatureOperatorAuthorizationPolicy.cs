namespace FVN_REGISTER.Core.Constants;

/// <summary>
/// Canonical authorization rule for designated feature operators.
/// Assignment never replaces RBAC; both capability and assignment are required.
/// </summary>
public static class FeatureOperatorAuthorizationPolicy
{
    public static bool CanOperate(bool hasRbacCapability, bool isAssignedOperator)
        => hasRbacCapability && isAssignedOperator;
}
