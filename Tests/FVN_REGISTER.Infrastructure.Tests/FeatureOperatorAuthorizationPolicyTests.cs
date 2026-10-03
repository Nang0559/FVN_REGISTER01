using FVN_REGISTER.Core.Constants;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class FeatureOperatorAuthorizationPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CanOperate_RequiresRbacAndOperatorAssignment(
        bool hasRbac,
        bool assigned,
        bool expected)
    {
        Assert.Equal(expected, FeatureOperatorAuthorizationPolicy.CanOperate(hasRbac, assigned));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void CanAssignOperator_RequiresEmployeeActiveUserAndRbac(
        bool employeeExists,
        bool activeUserExists,
        bool hasRbac,
        bool expected)
    {
        Assert.Equal(
            expected,
            FeatureOperatorAuthorizationPolicy.CanAssignOperator(
                employeeExists,
                activeUserExists,
                hasRbac));
    }

    [Fact]
    public void ExecutionAppealResolution_RejectsAssignedOperatorWhenRbacWasRevoked()
    {
        var hasCurrentRbac = false;
        var isAssignedOperator = true;

        Assert.False(
            FeatureOperatorAuthorizationPolicy.CanOperate(
                hasCurrentRbac,
                isAssignedOperator));
    }

    [Fact]
    public void ExecutionAppealResolution_AllowsOnlyCurrentRbacAndAssignment()
    {
        Assert.True(FeatureOperatorAuthorizationPolicy.CanOperate(true, true));
        Assert.False(FeatureOperatorAuthorizationPolicy.CanOperate(true, false));
        Assert.False(FeatureOperatorAuthorizationPolicy.CanOperate(false, true));
    }
}
