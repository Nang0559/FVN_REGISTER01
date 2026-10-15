using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Services;

namespace FVN_REGISTER.AI.Tests;

public sealed class RagAnswerServiceTests
{
    [Fact]
    public async Task AnswerAsync_ReturnsOnlyRetrievedCitations()
    {
        var chunkId = Guid.NewGuid().ToString();
        var search = new StubSearch([new KnowledgeSearchHit(chunkId, Guid.NewGuid().ToString(),
            "Policy.pdf", 3, "Employees must submit the approved form.", 0.91, "Policy.pdf, page 3")]);
        var model = new StubModelProvider($$"""{"status":"Supported","answer":"Submit the approved form.","citations":["{{chunkId}}"]}""");

        var result = await new RagAnswerService(search, model).AnswerAsync("FCCVN", "actor",
            ["EMPLOYEE"], "How do I submit?");

        Assert.Equal("Supported", result.Status);
        Assert.Single(result.Citations);
        Assert.Equal(chunkId, result.Citations[0].ChunkId);
    }

    [Fact]
    public async Task AnswerAsync_AbstainsWhenModelInventsCitation()
    {
        var search = new StubSearch([new KnowledgeSearchHit(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "Policy.pdf", 3, "Approved source text.", 0.9, "Policy.pdf, page 3")]);
        var model = new StubModelProvider("""{"status":"Supported","answer":"Unsupported claim.","citations":["not-a-retrieved-chunk"]}""");

        var result = await new RagAnswerService(search, model).AnswerAsync("FCCVN", "actor",
            ["EMPLOYEE"], "Question?");

        Assert.Equal("InvalidCitation", result.Status);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task AnswerAsync_AbstainsWhenNoAuthorizedScopesExist()
    {
        var search = new StubSearch([]);
        var model = new StubModelProvider("{}");

        var result = await new RagAnswerService(search, model).AnswerAsync("FCCVN", "actor",
            [], "Question?");

        Assert.Equal("Forbidden", result.Status);
        Assert.Empty(result.Citations);
        Assert.False(model.Called);
    }

    private sealed class StubSearch(IReadOnlyList<KnowledgeSearchHit> hits) : IKnowledgeSearchService
    {
        public Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(string tenantCode, string actorId,
            IReadOnlyCollection<string> allowedScopes, string query, int limit,
            CancellationToken cancellationToken = default) => Task.FromResult(hits);
    }

    private sealed class StubModelProvider(string output) : IAIModelProvider
    {
        public bool Called { get; private set; }
        public string ProviderName => "Test";
        public Task<AIModelResponse> GenerateAsync(AIModelRequest request, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(new AIModelResponse(output, "Test", "test-model", "stop", 10, 10, TimeSpan.Zero));
        }
    }
}
