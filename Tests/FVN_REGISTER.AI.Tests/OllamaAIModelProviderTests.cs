using System.Net;
using System.Text;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Tests;

public sealed class OllamaAIModelProviderTests
{
    [Fact]
    public async Task GenerateAsync_UsesConfiguredModelAndReturnsOllamaDraft()
    {
        var handler = new StubHandler("""{"message":{"role":"assistant","content":"HR cần kiểm tra bảng công trước khi xác nhận."},"done":true,"done_reason":"stop","prompt_eval_count":120,"eval_count":24}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var provider = new OllamaAIModelProvider(client,
            Options.Create(new AIPlatformOptions { Model = "qwen2.5:7b" }));

        var result = await provider.GenerateAsync(new AIModelRequest(
            "hr-reconciliation-reply-draft", """{"assessment":"Discrepancy"}""",
            "Chỉ dùng dữ kiện được cung cấp."));

        Assert.Equal("Ollama", result.Provider);
        Assert.Equal("qwen2.5:7b", result.Model);
        Assert.Contains("bảng công", result.Output);
        Assert.Equal("stop", result.FinishReason);
        Assert.Equal(120, result.InputTokens);
        Assert.Equal(24, result.OutputTokens);
        Assert.Contains("/api/chat", handler.RequestUri);
        Assert.Contains("\"num_ctx\":8192", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateAsync_RequestsJsonModeForStructuredTasks()
    {
        var handler = new StubHandler("""{"message":{"role":"assistant","content":"{}"},"done":true}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var provider = new OllamaAIModelProvider(client,
            Options.Create(new AIPlatformOptions { Model = "qwen2.5:7b" }));

        await provider.GenerateAsync(new AIModelRequest("schema-driven-extraction", "{}"));

        Assert.Contains("\"format\":\"json\"", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateAsync_GuideAnswersUseSmallContextPlainTextAndKeepTheModelLoaded()
    {
        var handler = new StubHandler("""{"message":{"role":"assistant","content":"ok"},"done":true}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var provider = new OllamaAIModelProvider(client,
            Options.Create(new AIPlatformOptions { Model = "qwen2.5:3b", ModelKeepAlive = "45m" }));

        await provider.GenerateAsync(new AIModelRequest("guide-answer", "input"));

        Assert.Contains("\"num_ctx\":4096", handler.RequestBody);
        Assert.Contains("\"keep_alive\":\"45m\"", handler.RequestBody);
        Assert.DoesNotContain("\"format\":\"json\"", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateAsync_RejectsMissingModelConfiguration()
    {
        using var client = new HttpClient(new StubHandler("{}")) { BaseAddress = new Uri("http://localhost:11434/") };
        var provider = new OllamaAIModelProvider(client, Options.Create(new AIPlatformOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GenerateAsync(new AIModelRequest("task", "input")));
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
