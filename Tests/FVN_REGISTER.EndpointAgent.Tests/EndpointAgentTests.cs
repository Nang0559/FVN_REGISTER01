using System.Text.Json;
using FVN_REGISTER.EndpointAgent;
using Xunit;

namespace FVN_REGISTER.EndpointAgent.Tests;

public sealed class EndpointAgentTests
{
    [Fact]
    public void Dpapi_roundtrip_preserves_secret_on_same_machine()
    {
        var secret = "pilot-secret-" + Guid.NewGuid().ToString("N");
        var protectedValue = WindowsSecretStore.Protect(secret);
        Assert.NotEqual(secret, protectedValue);
        Assert.Equal(secret, WindowsSecretStore.Unprotect(protectedValue));
    }

    [Fact]
    public void Agent_options_keep_lanscope_client_id()
    {
        var options = new EndpointAgentOptions { LanscopeClientId = "CAT-001" };
        Assert.Equal("CAT-001", options.LanscopeClientId);
    }

    [Fact]
    public void Bootstrap_json_contains_client_id()
    {
        var json = JsonSerializer.Serialize(new { deploymentId = 1, targetId = 2, enrollmentToken = "token", apiBaseUrl = "https://fvn.example", clientId = "CAT-001" });
        using var document = JsonDocument.Parse(json);
        Assert.Equal("CAT-001", document.RootElement.GetProperty("clientId").GetString());
    }
}
