using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Extraction;
using FVN_REGISTER.AI.Infrastructure.Services;
using UglyToad.PdfPig.Writer;

namespace FVN_REGISTER.AI.Tests;

public sealed class ScannedPdfOcrFallbackTests
{
    [Fact]
    public async Task ExtractTextAsync_PdfWithoutTextLayer_UsesOcrInsteadOfPageMarkersOnly()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        builder.AddPage(PageSize.A4);
        using var pdf = new MemoryStream(builder.Build());

        var text = await new LocalDocumentTextExtractor(new FakeOcr()).ExtractTextAsync(pdf, "application/pdf");

        Assert.StartsWith("[OCR]", text);
        Assert.Contains("OCR result", text);
    }

    private sealed class FakeOcr : IOcrTextExtractor
    {
        public Task<string> ExtractTextAsync(Stream content, string mediaType, CancellationToken cancellationToken = default) =>
            Task.FromResult("OCR result");
    }
}

public sealed class FvnDocumentReviewAuthorizationTests
{
    [Fact]
    public async Task AuthorizeReviewAsync_WithoutCompanyWidePublishRight_IsDenied()
    {
        var provider = new FvnDocumentReviewAuthorization(new FakeKnowledgeAccess(Array.Empty<string>()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            provider.AuthorizeReviewAsync("FCCVN", "7", Guid.NewGuid()));
    }

    [Fact]
    public async Task AuthorizeReviewAsync_WithCompanyWidePublishRight_IsAllowed()
    {
        var provider = new FvnDocumentReviewAuthorization(new FakeKnowledgeAccess(["fvn:staff"]));
        await provider.AuthorizeReviewAsync("FCCVN", "7", Guid.NewGuid());
    }

    [Fact]
    public async Task AuthorizeReviewAsync_WhenSecurityTablesUnavailable_FailsClosed()
    {
        var provider = new FvnDocumentReviewAuthorization(new FakeKnowledgeAccess(null));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AuthorizeReviewAsync("FCCVN", "7", Guid.NewGuid()));
        Assert.Equal("DOCUMENT_REVIEW_AUTHORIZATION_NOT_CONFIGURED", error.Message);
    }

    private sealed class FakeKnowledgeAccess(IReadOnlyCollection<string>? publicationScopes) : IAuthorizedKnowledgeAccessProvider
    {
        public Task<IReadOnlyCollection<string>> GetSearchScopesAsync(string tenantCode, string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

        public Task<IReadOnlyCollection<string>> GetPublicationScopesAsync(string tenantCode, string actorId, Guid documentId, Guid reviewId, CancellationToken cancellationToken = default) =>
            publicationScopes is null
                ? throw new InvalidOperationException("KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED")
                : Task.FromResult(publicationScopes);

        public Task AuthorizeUnpublishAsync(string tenantCode, string actorId, Guid documentId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
