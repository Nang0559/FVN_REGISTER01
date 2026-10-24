using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FVN_REGISTER.AI.Tests;

public sealed class GuideDocumentEvidenceProviderTests
{
    private static KnowledgeSearchHit Hit(int n, string text) =>
        new($"chunk-{n}", Guid.NewGuid().ToString(), $"doc{n}.pdf", n, text, 0.9, $"doc{n}.pdf, page {n}, chunk 0");

    [Fact]
    public async Task ReturnsAtMostThreeTrimmedPassagesWithDocumentTags()
    {
        var search = new FakeSearch(Hit(1, new string('a', 2_000)), Hit(2, "b"), Hit(3, "c"), Hit(4, "d"));
        var provider = new GuideDocumentEvidenceProvider(search, NullLogger<GuideDocumentEvidenceProvider>.Instance, new FakeAccess("scope-a"));

        var result = await provider.FindAsync("tenant", "42", "hạn mức OT");

        Assert.Equal(new[] { "D1", "D2", "D3" }, result.Select(r => r.Evidence.Id));
        Assert.All(result, r => Assert.True(r.Evidence.Text.Length <= 900));
        Assert.Equal(new[] { "scope-a" }, search.LastScopes);
        Assert.Equal("doc1.pdf", result[0].Source.FileName);
    }

    [Fact]
    public async Task NoScopes_MeansNoSearchAtAll()
    {
        var search = new FakeSearch(Hit(1, "x"));
        var provider = new GuideDocumentEvidenceProvider(search, NullLogger<GuideDocumentEvidenceProvider>.Instance, new FakeAccess());

        Assert.Empty(await provider.FindAsync("tenant", "42", "câu hỏi"));
        Assert.False(search.Called);
    }

    [Fact]
    public async Task WithoutAccessProvider_ReturnsNothing()
    {
        var provider = new GuideDocumentEvidenceProvider(new FakeSearch(Hit(1, "x")), NullLogger<GuideDocumentEvidenceProvider>.Instance);

        Assert.Empty(await provider.FindAsync("tenant", "42", "câu hỏi"));
    }

    [Fact]
    public async Task SearchFailure_IsSwallowedSoTheGuideAnswerStillWorks()
    {
        var provider = new GuideDocumentEvidenceProvider(new FakeSearch { Throw = true }, NullLogger<GuideDocumentEvidenceProvider>.Instance, new FakeAccess("scope-a"));

        Assert.Empty(await provider.FindAsync("tenant", "42", "câu hỏi"));
    }

    private sealed class FakeAccess(params string[] scopes) : IAuthorizedKnowledgeAccessProvider
    {
        public Task<IReadOnlyCollection<string>> GetSearchScopesAsync(string tenantCode, string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>(scopes);

        // Publication and unpublish authorization are not used by the guide assistant.
        public Task<IReadOnlyCollection<string>> GetPublicationScopesAsync(string tenantCode, string actorId, Guid documentId, Guid reviewId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AuthorizeUnpublishAsync(string tenantCode, string actorId, Guid documentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSearch(params KnowledgeSearchHit[] hits) : IKnowledgeSearchService
    {
        public bool Called { get; private set; }
        public bool Throw { get; init; }
        public IReadOnlyCollection<string>? LastScopes { get; private set; }

        public Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(string tenantCode, string actorId, IReadOnlyCollection<string> allowedScopes, string query, int limit, CancellationToken cancellationToken = default)
        {
            Called = true;
            LastScopes = allowedScopes;
            if (Throw) throw new InvalidOperationException("boom");
            return Task.FromResult<IReadOnlyList<KnowledgeSearchHit>>(hits);
        }
    }
}
