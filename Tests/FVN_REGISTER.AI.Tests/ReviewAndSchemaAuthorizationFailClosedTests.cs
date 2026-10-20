using FVN_REGISTER.AI.Infrastructure.Services;

namespace FVN_REGISTER.AI.Tests;

public sealed class ReviewAndSchemaAuthorizationFailClosedTests
{
    [Fact]
    public async Task Review_FailsClosedWhenReviewerAdapterIsMissing()
    {
        var provider = new UnconfiguredDocumentReviewProvider();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AuthorizeReviewAsync("FCCVN", "actor-001", Guid.NewGuid()));

        Assert.Equal("DOCUMENT_REVIEW_AUTHORIZATION_NOT_CONFIGURED", error.Message);
    }

    [Fact]
    public async Task SchemaDraft_FailsClosedWhenAdminAdapterIsMissing()
    {
        var provider = new UnconfiguredSchemaManagementProvider();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AuthorizeDraftAsync("FCCVN", "actor-001", "HR", "LEAVE"));

        Assert.Equal("SCHEMA_ADMIN_AUTHORIZATION_NOT_CONFIGURED", error.Message);
    }

    [Fact]
    public async Task SchemaPublish_FailsClosedWhenAdminAdapterIsMissing()
    {
        var provider = new UnconfiguredSchemaManagementProvider();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AuthorizePublishAsync("FCCVN", "actor-001", "HR", "LEAVE", 1));

        Assert.Equal("SCHEMA_ADMIN_AUTHORIZATION_NOT_CONFIGURED", error.Message);
    }
}
