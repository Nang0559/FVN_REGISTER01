using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Services;
using Xunit;

namespace FVN_REGISTER.AI.Tests;

public sealed class FvnBusinessGatewayFailClosedTests
{
    private readonly UnconfiguredFvnBusinessGateway _gateway = new();

    [Fact]
    public async Task Context_provider_fails_closed_when_host_adapter_is_missing()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gateway.GetContextAsync("FCCVN", "42"));

        Assert.Equal("FVN_BUSINESS_GATEWAY_NOT_CONFIGURED", exception.Message);
    }

    [Fact]
    public async Task Tool_catalog_fails_closed_when_host_adapter_is_missing()
    {
        var context = new FvnBusinessUserContext(
            "FCCVN", "42", null, null, null,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            DateTimeOffset.UtcNow, "test");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gateway.GetAvailableToolsAsync(context));

        Assert.Equal("FVN_BUSINESS_GATEWAY_NOT_CONFIGURED", exception.Message);
    }

    [Fact]
    public async Task Tool_execution_fails_closed_when_host_adapter_is_missing()
    {
        var invocation = new FvnBusinessToolInvocation(
            "HR.EMPLOYEE.READ", "{}", null, null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gateway.ExecuteAsync("FCCVN", "42", invocation));

        Assert.Equal("FVN_BUSINESS_GATEWAY_NOT_CONFIGURED", exception.Message);
    }
}
