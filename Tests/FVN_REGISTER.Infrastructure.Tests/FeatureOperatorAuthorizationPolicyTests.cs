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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CanSelectOperator_RequiresRbacAndTargetScope(
        bool hasRbac,
        bool hasTargetScope,
        bool expected)
    {
        Assert.Equal(
            expected,
            FeatureOperatorAuthorizationPolicy.CanSelectOperator(
                hasRbac,
                hasTargetScope));
    }

    [Fact]
    public void AssignmentCreation_RejectsMissingActiveUserOrRbac()
    {
        Assert.False(FeatureOperatorAuthorizationPolicy.CanAssignOperator(true, false, true));
        Assert.False(FeatureOperatorAuthorizationPolicy.CanAssignOperator(true, true, false));
    }

    [Fact]
    public void ExecutionAppealResolution_SkipsAssignedOperatorWhoseRbacWasRevoked()
    {
        var selected = FeatureOperatorAuthorizationPolicy.SelectFirstEligibleOperator(
            new[]
            {
                (101, "E0101", false),
                (202, "E0202", true)
            });

        Assert.Equal((202, "E0202"), selected);
    }

    [Fact]
    public void ExecutionAppealResolution_ReturnsNoOperatorWhenAllAssignmentsAreInvalid()
    {
        var selected = FeatureOperatorAuthorizationPolicy.SelectFirstEligibleOperator(
            new[]
            {
                (101, "E0101", false),
                (202, "E0202", false)
            });

        Assert.Null(selected);
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