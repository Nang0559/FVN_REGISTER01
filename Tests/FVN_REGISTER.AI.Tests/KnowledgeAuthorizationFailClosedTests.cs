using FVN_REGISTER.AI.Infrastructure.Services;

namespace FVN_REGISTER.AI.Tests;

public sealed class KnowledgeAuthorizationFailClosedTests
{
    [Fact]
    public async Task SearchScopes_FailClosedWhenGatewayAdapterIsMissing()
    {
        var provider = new UnconfiguredAuthorizedKnowledgeAccessProvider();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetSearchScopesAsync("FCCVN", "employee-001"));

        Assert.Equal("KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED", error.Message);
    }

    [Fact]
    public async Task PublicationScopes_FailClosedWhenGatewayAdapterIsMissing()
    {
        var provider = new UnconfiguredAuthorizedKnowledgeAccessProvider();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetPublicationScopesAsync("FCCVN", "hr-001", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED", error.Message);
    }

    [Fact]
    public async Task Unpublish_FailsClosedWhenGatewayAdapterIsMissing()
    {
        var provider = new UnconfiguredAuthorizedKnowledgeAccessProvider();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AuthorizeUnpublishAsync("FCCVN", "hr-001", Guid.NewGuid()));

        Assert.Equal("KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED", error.Message);
    }
}
