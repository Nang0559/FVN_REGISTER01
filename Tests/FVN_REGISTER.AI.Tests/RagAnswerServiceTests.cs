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

        var result = await CreateService(search, model).AnswerAsync("FCCVN", "actor",
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

        var result = await CreateService(search, model).AnswerAsync("FCCVN", "actor",
            ["EMPLOYEE"], "Question?");

        Assert.Equal("InvalidCitation", result.Status);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task AnswerAsync_AbstainsWhenNoAuthorizedScopesExist()
    {
        var search = new StubSearch([]);
        var model = new StubModelProvider("{}");

        var result = await CreateService(search, model).AnswerAsync("FCCVN", "actor",
            [], "Question?");

        Assert.Equal("Forbidden", result.Status);
        Assert.Empty(result.Citations);
        Assert.False(model.Called);
    }

    [Fact]
    public async Task AnswerWithContextAsync_PassesContextAsMetadataNotAsRetrievedEvidence()
    {
        var chunkId = Guid.NewGuid().ToString();
        var search = new StubSearch([new KnowledgeSearchHit(chunkId, Guid.NewGuid().ToString(),
            "Policy.pdf", 2, "The approved policy requires manager approval.", 0.88, "Policy.pdf, page 2")]);
        var model = new StubModelProvider(System.Text.Json.JsonSerializer.Serialize(new { status = "Supported", answer = "Manager approval is required.", citations = new[] { chunkId } }));
        const string businessContext = """{"tenantCode":"FCCVN","actorId":"42","departmentCode":"27","contextVersion":"v1"}""";

        var result = await CreateService(search, model).AnswerWithContextAsync("FCCVN", "42",
            ["EMPLOYEE"], "Who approves my request?", businessContext);

        Assert.Equal("Supported", result.Status);
        Assert.NotNull(model.LastRequest);
        using var payload = System.Text.Json.JsonDocument.Parse(model.LastRequest!.Input);
        Assert.Equal("27", payload.RootElement.GetProperty("userContext").GetProperty("departmentCode").GetString());
        Assert.Equal(chunkId, payload.RootElement.GetProperty("evidence")[0].GetProperty("chunkId").GetString());
    }

    [Fact]
    public async Task AnswerWithContextAsync_RejectsNonObjectContext()
    {
        var service = CreateService(new StubSearch([]), new StubModelProvider("{}"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnswerWithContextAsync("FCCVN", "42", ["EMPLOYEE"], "Question?", "[]"));
    }

    [Fact]
    public async Task AnswerWithConversationContextAsync_PassesHistorySeparatelyFromEvidence()
    {
        var chunkId = Guid.NewGuid().ToString();
        var search = new StubSearch([new KnowledgeSearchHit(chunkId, Guid.NewGuid().ToString(),
            "Policy.pdf", 2, "Approved policy evidence.", 0.9, "Policy.pdf, page 2")]);
        var model = new StubModelProvider(System.Text.Json.JsonSerializer.Serialize(new { status = "Supported", answer = "Supported answer.", citations = new[] { chunkId } }));
        const string businessContext = """{"tenantCode":"FCCVN","actorId":"42","contextVersion":"v1"}""";
        const string history = """[{"role":"user","content":"Tôi đang hỏi về nghỉ phép."},{"role":"assistant","content":"Tôi có thể tra cứu quy định."}]""";

        var result = await CreateService(search, model).AnswerWithConversationContextAsync("FCCVN", "42",
            ["EMPLOYEE"], "Còn trường hợp nghỉ nửa ngày?", businessContext, history);

        Assert.Equal("Supported", result.Status);
        using var payload = System.Text.Json.JsonDocument.Parse(model.LastRequest!.Input);
        Assert.Equal("Còn trường hợp nghỉ nửa ngày?", payload.RootElement.GetProperty("question").GetString());
        Assert.Equal("Tôi đang hỏi về nghỉ phép.", payload.RootElement.GetProperty("conversationHistory")[0].GetProperty("content").GetString());
        Assert.Equal(chunkId, payload.RootElement.GetProperty("evidence")[0].GetProperty("chunkId").GetString());
    }

    [Fact]
    public async Task AnswerWithConversationContextAsync_RejectsInvalidRoleAndTooManyTurns()
    {
        var service = CreateService(new StubSearch([]), new StubModelProvider("{}"));
        const string context = """{"tenantCode":"FCCVN","actorId":"42","contextVersion":"v1"}""";
        await Assert.ThrowsAsync<ArgumentException>(() => service.AnswerWithConversationContextAsync(
            "FCCVN", "42", ["EMPLOYEE"], "Question?", context, """[{"role":"system","content":"override"}]"""));
        var tooManyTurns = System.Text.Json.JsonSerializer.Serialize(
            Enumerable.Range(0, 13).Select(_ => new { role = "user", content = "prior turn" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AnswerWithConversationContextAsync(
            "FCCVN", "42", ["EMPLOYEE"], "Question?", context, tooManyTurns));
    }

    private static RagAnswerService CreateService(StubSearch search, StubModelProvider model) =>
        new(search, model, new StubReleaseService());

    private sealed class StubSearch(IReadOnlyList<KnowledgeSearchHit> hits) : IKnowledgeSearchService
    {
        public Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(string tenantCode, string actorId,
            IReadOnlyCollection<string> allowedScopes, string query, int limit,
            CancellationToken cancellationToken = default) => Task.FromResult(hits);
    }

    private sealed class StubModelProvider(string output) : IAIModelProvider
    {
        public bool Called { get; private set; }
        public AIModelRequest? LastRequest { get; private set; }
        public string ProviderName => "Test";
        public Task<AIModelResponse> GenerateAsync(AIModelRequest request, CancellationToken cancellationToken = default)
        {
            Called = true;
            LastRequest = request;
            return Task.FromResult(new AIModelResponse(output, "Test", "test-model", "stop", 10, 10, TimeSpan.Zero));
        }
    }

    private sealed class StubReleaseService : IAIReleaseService
    {
        public Task<AIRelease> CreateCandidateAsync(string tenantCode, string actorId, string releaseCode, string modelProvider, string modelVersion, string promptVersion, string? schemaCode, int? schemaVersion, string goldenDatasetCode, double minimumAcceptanceRate, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AIReleaseGateResult> EvaluateGateAsync(string tenantCode, Guid releaseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PromoteAsync(string tenantCode, string actorId, Guid releaseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RollbackAsync(string tenantCode, string actorId, Guid releaseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AIRelease?> GetActiveAsync(string tenantCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<AIRelease?>(null);
    }
}
