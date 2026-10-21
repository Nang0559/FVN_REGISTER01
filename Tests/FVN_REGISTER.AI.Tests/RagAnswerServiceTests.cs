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
    public async Task AnswerWithConversationContextAsync_PreservesVietnameseHistoryWithoutUnicodeExpansion()
    {
        var chunkId = Guid.NewGuid().ToString();
        var search = new StubSearch([new KnowledgeSearchHit(chunkId, Guid.NewGuid().ToString(),
            "Policy.pdf", 1, "Quy định nghỉ phép đã được phê duyệt.", 0.9, "Policy.pdf, page 1")]);
        var model = new StubModelProvider(System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "Supported", answer = "Có thể đăng ký nghỉ phép.", citations = new[] { chunkId }
        }));
        const string context = """{"tenantCode":"FCCVN","actorId":"42","contextVersion":"v1"}""";
        var history = System.Text.Json.JsonSerializer.Serialize(
            Enumerable.Range(0, 8).Select(i => new { role = i % 2 == 0 ? "user" : "assistant", content = string.Concat(Enumerable.Repeat("Tôi cần hướng dẫn đăng ký nghỉ phép có dấu tiếng Việt. ", 20)) }),
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var result = await CreateService(search, model).AnswerWithConversationContextAsync(
            "FCCVN", "42", ["EMPLOYEE"], "Còn trường hợp nghỉ nửa ngày?", context, history);

        Assert.Equal("Supported", result.Status);
        Assert.Contains("nghỉ phép", model.LastRequest!.Input, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0111", model.LastRequest.Input, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnswerWithConversationContextAsync_KeepsOnlyNewestTurnsWithinHistoryBudget()
    {
        var chunkId = Guid.NewGuid().ToString();
        var search = new StubSearch([new KnowledgeSearchHit(chunkId, Guid.NewGuid().ToString(),
            "Policy.pdf", 1, "Quy định nghỉ phép.", 0.9, "Policy.pdf, page 1")]);
        var model = new StubModelProvider(System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "Supported", answer = "Có.", citations = new[] { chunkId }
        }));
        const string context = """{"tenantCode":"FCCVN","actorId":"42","contextVersion":"v1"}""";
        var history = System.Text.Json.JsonSerializer.Serialize(
            Enumerable.Range(0, 8).Select(i => new { role = i % 2 == 0 ? "user" : "assistant", content = $"Lượt {i}: " + new string('ạ', 2400) }),
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var result = await CreateService(search, model).AnswerWithConversationContextAsync(
            "FCCVN", "42", ["EMPLOYEE"], "Còn nửa ngày?", context, history);

        Assert.Equal("Supported", result.Status);
        using var payload = System.Text.Json.JsonDocument.Parse(model.LastRequest!.Input);
        var kept = payload.RootElement.GetProperty("conversationHistory");
        Assert.InRange(kept.GetArrayLength(), 1, 3);
        Assert.StartsWith("Lượt 7:", kept[kept.GetArrayLength() - 1].GetProperty("content").GetString(), StringComparison.Ordinal);
        Assert.True(kept.EnumerateArray().Sum(x => x.GetProperty("content").GetString()!.Length) <= 8_000 + 2_500);
    }

    [Fact]
    public async Task AnswerAsync_BoundsTotalEvidenceCharactersBeforeModelCall()
    {
        var chunkIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString()).ToArray();
        var hits = chunkIds.Select(id => new KnowledgeSearchHit(id, Guid.NewGuid().ToString(),
            "Policy.pdf", 1, new string('A', 6000), 0.9, "Policy.pdf, page 1")).ToArray();
        var search = new StubSearch(hits);
        var model = new StubModelProvider(System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "Supported", answer = "Supported.", citations = new[] { chunkIds[0], chunkIds[1] }
        }));

        var result = await CreateService(search, model).AnswerAsync("FCCVN", "42", ["EMPLOYEE"], "Question?");

        Assert.Equal("Supported", result.Status);
        using var payload = System.Text.Json.JsonDocument.Parse(model.LastRequest!.Input);
        var evidenceLength = payload.RootElement.GetProperty("evidence").EnumerateArray()
            .Sum(x => x.GetProperty("content").GetString()!.Length);
        Assert.InRange(evidenceLength, 1, 9000);
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
