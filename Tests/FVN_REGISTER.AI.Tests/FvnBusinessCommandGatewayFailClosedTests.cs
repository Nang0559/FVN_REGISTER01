using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Services;

namespace FVN_REGISTER.AI.Tests;

public sealed class FvnBusinessCommandGatewayFailClosedTests
{
    private readonly UnconfiguredFvnBusinessCommandGateway _gateway = new();

    [Fact]
    public async Task Preview_fails_closed_without_trusted_host_adapter()
    {
        var request = new FvnBusinessCommandPreviewRequest("HR.LEAVE.CREATE", "{}", null, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gateway.PreviewAsync("FCCVN", "actor-1", request));

        Assert.Equal("FVN_BUSINESS_COMMAND_GATEWAY_NOT_CONFIGURED", error.Message);
    }

    [Fact]
    public async Task Confirmed_execution_fails_closed_without_trusted_host_adapter()
    {
        var confirmation = new FvnBusinessCommandConfirmation(
            "preview-1", "opaque-token", "human-confirmation-1", "idempotency-1", null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gateway.ExecuteConfirmedAsync("FCCVN", "actor-1", confirmation));

        Assert.Equal("FVN_BUSINESS_COMMAND_GATEWAY_NOT_CONFIGURED", error.Message);
    }
}
