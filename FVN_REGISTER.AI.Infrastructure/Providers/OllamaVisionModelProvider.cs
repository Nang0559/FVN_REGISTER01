using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace FVN_REGISTER.AI.Infrastructure.Providers;

/// <summary>Optional Ollama multimodal adapter. It is used only when AIPlatform:VisionModel is configured.</summary>
public sealed class OllamaVisionModelProvider(HttpClient httpClient, IConfiguration configuration) : IAIVisionModelProvider
{
    private readonly string? _model = configuration["AIPlatform:VisionModel"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "OllamaVision";
    public string ModelVersion => string.IsNullOrWhiteSpace(_model) ? "Unconfigured" : _model.Trim();

    public async Task<AIModelResponse> AnalyzeImageAsync(Stream image, string mediaType, AIModelRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_model)) throw new InvalidOperationException("VISION_MODEL_NOT_CONFIGURED");
        if (!mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("VISION_MEDIA_TYPE_NOT_SUPPORTED");
        using var memory = new MemoryStream();
        await image.CopyToAsync(memory, cancellationToken);
        if (memory.Length > 15 * 1024 * 1024) throw new InvalidDataException("VISION_IMAGE_TOO_LARGE");
        var base64 = Convert.ToBase64String(memory.ToArray());
        var prompt = string.IsNullOrWhiteSpace(request.Input) ? "Inspect the image and return only the requested structured data." : request.Input;
        var payload = new OllamaChatRequest(_model.Trim(), false,
            new[] { new OllamaMessage("system", request.SystemInstruction ?? "Read the supplied document image faithfully. Do not invent missing values."), new OllamaMessage("user", prompt, new[] { base64 }) },
            new OllamaOptions(0.05, 8192, 1600), "json");
        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.PostAsJsonAsync("api/chat", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
        stopwatch.Stop();
        var output = result?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("OLLAMA_VISION_EMPTY_RESPONSE");
        return new AIModelResponse(output, ProviderName, _model.Trim(), result?.DoneReason, result?.PromptEvalCount, result?.EvalCount, stopwatch.Elapsed);
    }

    private sealed record OllamaChatRequest(string Model, bool Stream, IReadOnlyList<OllamaMessage> Messages, OllamaOptions Options, string? Format);
    private sealed record OllamaMessage(string Role, string Content, [property: JsonPropertyName("images")] IReadOnlyList<string>? Images = null);
    private sealed record OllamaOptions(double Temperature, [property: JsonPropertyName("num_ctx")] int NumCtx, [property: JsonPropertyName("num_predict")] int NumPredict);
    private sealed record OllamaChatResponse(OllamaMessage? Message, bool Done, [property: JsonPropertyName("done_reason")] string? DoneReason, [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount, [property: JsonPropertyName("eval_count")] int? EvalCount);
}
