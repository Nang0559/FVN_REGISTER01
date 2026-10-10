using System.Linq;
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

    // ---- capability-granting operators (Execution.Review): role grant OR assignment ----

    [Theory]
    [InlineData(false, false, false)] // no effective capability (neither role nor assignment)
    [InlineData(true, false, true)]   // effective capability alone is enough (role OR assignment)
    [InlineData(true, true, true)]
    public void CanOperate_ExecutionReview_IsGrantedByEffectiveCapabilityAlone(
        bool effectiveCapability, bool assigned, bool expected)
    {
        Assert.Equal(
            expected,
            FeatureOperatorAuthorizationPolicy.CanOperate(
                SecurityFunctionCodes.ExecutionReview, effectiveCapability, assigned));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void CanOperate_LegacyFunctions_StillRequireRbacAndAssignment(
        bool hasRbac, bool assigned, bool expected)
    {
        Assert.Equal(
            expected,
            FeatureOperatorAuthorizationPolicy.CanOperate(
                SecurityFunctionCodes.ExecutionPolicyManage, hasRbac, assigned));
    }

    [Fact]
    public void CanAssignOperator_ExecutionReview_DoesNotRequireExistingRoleGrant()
    {
        Assert.True(FeatureOperatorAuthorizationPolicy.CanAssignOperator(
            SecurityFunctionCodes.ExecutionReview, true, true, false));
        Assert.False(FeatureOperatorAuthorizationPolicy.CanAssignOperator(
            SecurityFunctionCodes.ExecutionReview, true, false, false));
        Assert.False(FeatureOperatorAuthorizationPolicy.CanAssignOperator(
            SecurityFunctionCodes.ExecutionPolicyManage, true, true, false));
    }

    [Theory]
    [InlineData(true, false, true)]   // SuperAdmin may always assign
    [InlineData(false, true, true)]   // assigner holds the capability
    [InlineData(false, false, false)] // cannot grant what you do not have
    public void CanGrantAssignment_RequiresHoldingTheCapability(bool superAdmin, bool holds, bool expected)
    {
        Assert.Equal(expected, FeatureOperatorAuthorizationPolicy.CanGrantAssignment(superAdmin, holds));
    }

    [Fact]
    public void ScopeRank_OrdersAllAboveDepartmentAboveOwn()
    {
        Assert.True(FeatureOperatorAuthorizationPolicy.ScopeRank("All") > FeatureOperatorAuthorizationPolicy.ScopeRank("Department"));
        Assert.True(FeatureOperatorAuthorizationPolicy.ScopeRank("Department") > FeatureOperatorAuthorizationPolicy.ScopeRank("Own"));
        Assert.True(FeatureOperatorAuthorizationPolicy.ScopeRank("Own") > FeatureOperatorAuthorizationPolicy.ScopeRank("None"));
    }

    [Fact]
    public void Catalog_OnlyExecutionReviewGrantsCapabilityByAssignment()
    {
        Assert.True(FeatureOperatorCatalog.AssignmentGrantsCapability(SecurityFunctionCodes.ExecutionReview));
        Assert.False(FeatureOperatorCatalog.AssignmentGrantsCapability(SecurityFunctionCodes.ExecutionPolicyManage));
        Assert.False(FeatureOperatorCatalog.AssignmentGrantsCapability(SecurityFunctionCodes.PublicFormExport));
        Assert.Equal(
            new[] { SecurityFunctionCodes.ExecutionReview },
            FeatureOperatorCatalog.CapabilityGrantingFunctionCodes.ToArray());
    }

    [Theory]
    [InlineData(SecurityFunctionCodes.EquipmentView)]
    [InlineData(SecurityFunctionCodes.EquipmentImport)]
    public void EquipmentOperators_AreModuleWideAndStillRequireRbac(int functionCode)
    {
        Assert.True(FeatureOperatorCatalog.TryGetResourceType(functionCode, out var resourceType));
        Assert.Equal(FeatureOperatorCatalog.Equipment, resourceType);
        Assert.True(FeatureOperatorCatalog.IsModuleWide(functionCode));
        Assert.False(FeatureOperatorCatalog.AssignmentGrantsCapability(functionCode));
    }
}
