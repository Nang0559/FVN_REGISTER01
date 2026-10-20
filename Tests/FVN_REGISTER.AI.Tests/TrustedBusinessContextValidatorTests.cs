using System.Text.Json;
using FVN_REGISTER.AI.Api.Security;
using Xunit;

namespace FVN_REGISTER.AI.Tests;

public sealed class TrustedBusinessContextValidatorTests
{
    private const string ValidContext = """
        {
          "tenantCode":"FCCVN",
          "actorId":"42",
          "departmentCode":"27",
          "positionCode":"LEADER",
          "roleCodes":["3","7"],
          "permissionCodes":["FVN_FUNCTION:101"],
          "functionScopeCodes":["101:DEPT"],
          "managedScopeCodes":["DEPT:27"],
          "contextVersion":"v1"
        }
        """;

    [Fact]
    public void Accepts_minimized_context_when_signed_identity_matches()
    {
        using var document = JsonDocument.Parse(ValidContext);

        var valid = TrustedBusinessContextValidator.TryValidate(
            document.RootElement, "FCCVN", "42", out var error);

        Assert.True(valid);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("OTHER", "42")]
    [InlineData("FCCVN", "different-actor")]
    public void Rejects_context_for_another_tenant_or_actor(string tenant, string actor)
    {
        using var document = JsonDocument.Parse(ValidContext);

        var valid = TrustedBusinessContextValidator.TryValidate(
            document.RootElement, tenant, actor, out var error);

        Assert.False(valid);
        Assert.Contains("identity", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_non_string_permission_entries()
    {
        const string json = """
            {
              "tenantCode":"FCCVN","actorId":"42","contextVersion":"v1",
              "roleCodes":[],"permissionCodes":[{"permission":"ADMIN"}],
              "functionScopeCodes":[],"managedScopeCodes":[]
            }
            """;
        using var document = JsonDocument.Parse(json);

        Assert.False(TrustedBusinessContextValidator.TryValidate(
            document.RootElement, "FCCVN", "42", out _));
    }

    [Fact]
    public void Rejects_duplicate_scope_codes()
    {
        const string json = """
            {
              "tenantCode":"FCCVN","actorId":"42","contextVersion":"v1",
              "roleCodes":[],"permissionCodes":[],
              "functionScopeCodes":[],"managedScopeCodes":["DEPT:27","dept:27"]
            }
            """;
        using var document = JsonDocument.Parse(json);

        Assert.False(TrustedBusinessContextValidator.TryValidate(
            document.RootElement, "FCCVN", "42", out _));
    }

    [Fact]
    public void Rejects_missing_required_code_arrays()
    {
        const string json = """
            {"tenantCode":"FCCVN","actorId":"42","contextVersion":"v1"}
            """;
        using var document = JsonDocument.Parse(json);

        Assert.False(TrustedBusinessContextValidator.TryValidate(
            document.RootElement, "FCCVN", "42", out _));
    }
}
