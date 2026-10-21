using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Infrastructure.Providers;

/// <summary>Local Ollama chat adapter. The caller owns grounding, authorization and human approval.</summary>
public sealed class OllamaAIModelProvider(HttpClient httpClient, IOptions<AIPlatformOptions> options) : IAIModelProvider, IAIModelProvenance
{
    private readonly AIPlatformOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "Ollama";
    public string ModelVersion => string.IsNullOrWhiteSpace(_options.Model) ? "Unconfigured" : _options.Model.Trim();

    public async Task<AIModelResponse> GenerateAsync(AIModelRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Model))
            throw new InvalidOperationException("OLLAMA_MODEL_NOT_CONFIGURED");
        if (string.IsNullOrWhiteSpace(request.Input) || request.Input.Length > 120_000)
            throw new ArgumentException("Model input is empty or exceeds the 120 KB safety limit.", nameof(request));

        var model = string.IsNullOrWhiteSpace(request.ModelHint) ? _options.Model : request.ModelHint;
        var payload = new OllamaChatRequest(model, false,
            new[]
            {
                new OllamaMessage("system", request.SystemInstruction ?? "Answer using only the supplied context."),
                new OllamaMessage("user", request.Input)
            },
            new OllamaOptions(0.1, 8192, 1200),
            request.Task is "document-classification" or "schema-driven-extraction" or "rag-grounded-answer" ? "json" : null);

        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.PostAsJsonAsync("api/chat", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
        stopwatch.Stop();
        var output = result?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("OLLAMA_EMPTY_RESPONSE");

        return new AIModelResponse(output, ProviderName, model,
            result?.DoneReason, result?.PromptEvalCount, result?.EvalCount, stopwatch.Elapsed);
    }

    private sealed record OllamaChatRequest(string Model, bool Stream, IReadOnlyList<OllamaMessage> Messages, OllamaOptions Options, string? Format);
    private sealed record OllamaMessage(string Role, string Content);
    private sealed record OllamaOptions(double Temperature, [property: JsonPropertyName("num_ctx")] int NumCtx, [property: JsonPropertyName("num_predict")] int NumPredict);
    private sealed record OllamaChatResponse(OllamaMessage? Message, bool Done,
        [property: JsonPropertyName("done_reason")] string? DoneReason,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount);
}
