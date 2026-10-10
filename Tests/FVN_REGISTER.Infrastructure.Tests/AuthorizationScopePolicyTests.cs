using FVN_REGISTER.Core.Constants;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class AuthorizationScopePolicyTests
{
    [Fact]
    public void ResolveEffectiveScope_UsesMostPermissiveConfiguredScope()
    {
        Assert.Equal(
            AuthorizationScopeCodes.All,
            AuthorizationScopePolicy.ResolveEffectiveScope(new[]
            {
                AuthorizationScopeCodes.Own,
                AuthorizationScopeCodes.Department,
                AuthorizationScopeCodes.All
            }));
    }

    [Fact]
    public void CanAccess_DepartmentScope_OnlyMatchesDepartment()
    {
        Assert.True(AuthorizationScopePolicy.CanAccess(
            AuthorizationScopeCodes.Department,
            "HR01",
            1,
            "EMP99",
            1));

        Assert.False(AuthorizationScopePolicy.CanAccess(
            AuthorizationScopeCodes.Department,
            "HR01",
            1,
            "EMP99",
            2));
    }

    [Fact]
    public void CanAccess_OwnAndEmployeeScopes_MatchEmployee()
    {
        Assert.True(AuthorizationScopePolicy.CanAccess(
            AuthorizationScopeCodes.Own,
            "EMP01",
            1,
            "EMP01",
            2));

        Assert.True(AuthorizationScopePolicy.CanAccess(
            AuthorizationScopeCodes.Employee,
            "EMP01",
            1,
            "EMP01",
            2));

        Assert.False(AuthorizationScopePolicy.CanAccess(
            AuthorizationScopeCodes.Own,
            "EMP01",
            1,
            "EMP02",
            1));
    }

    [Fact]
    public void CanAccess_NoneScope_DeniesAccess()
    {
        Assert.False(AuthorizationScopePolicy.CanAccess(
            AuthorizationScopeCodes.None,
            "EMP01",
            1,
            "EMP01",
            1));
    }
}
