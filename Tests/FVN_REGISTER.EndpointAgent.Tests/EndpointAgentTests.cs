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
    public void Serial_placeholder_is_rejected()
    {
        Assert.False(FVN_REGISTER.Contract.Security.LanscopeSecurityPolicy.IsMeaningfulSerial("To Be Filled By O.E.M."));
        Assert.False(FVN_REGISTER.Contract.Security.LanscopeSecurityPolicy.IsMeaningfulSerial("Default String"));
        Assert.False(FVN_REGISTER.Contract.Security.LanscopeSecurityPolicy.IsMeaningfulSerial("00000000"));
        Assert.True(FVN_REGISTER.Contract.Security.LanscopeSecurityPolicy.IsMeaningfulSerial("PF4ABC123"));
    }

    [Theory]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    [InlineData(409, false)]
    [InlineData(429, false)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    public void Enrollment_retry_policy_retries_only_server_errors(int statusCode, bool expected)
    {
        Assert.Equal(expected, FVN_REGISTER.Contract.Security.LanscopeSecurityPolicy.ShouldRetryEnrollmentStatus(statusCode));
    }

