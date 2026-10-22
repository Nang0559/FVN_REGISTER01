using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Authenticated diagnostics gateway for AI Platform. Service credentials remain server-side.
/// Access is restricted to users with the system security-view permission.
/// </summary>
[Authorize]
[ApiController]
[Route("api/ai/platform-test")]
public sealed class AIPlatformDiagnosticsController : BaseApiController
{
    private readonly IAuthorizationService _authorization;
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;

    public AIPlatformDiagnosticsController(
        IAuthorizationService authorization,
        IHttpClientFactory clients,
        IConfiguration configuration,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<AIPlatformDiagnosticsController> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _authorization = authorization;
        _clients = clients;
        _configuration = configuration;
    }

    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness(CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { status = "not-ready", error = "AIPlatform:BaseUrl chưa được cấu hình." });

        using var ready = await SendServiceHealthAsync(new Uri(baseUri, "/health/ready"), ct);
        var readyBody = await ready.Content.ReadAsStringAsync(ct);
        string capabilities = "";
        try
        {
            using var caps = await SendServiceHealthAsync(new Uri(baseUri, "/api/ai/capabilities"), ct);
            capabilities = await caps.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException) { }
        return Ok(new
        {
            apiReachable = !readyBody.Contains("Không kết nối được AI API.", StringComparison.OrdinalIgnoreCase) && !readyBody.Contains("AI API quá thời gian", StringComparison.OrdinalIgnoreCase),
            readinessHttpStatus = (int)ready.StatusCode,
            readiness = TryParseJson(readyBody),
            capabilities = TryParseJson(capabilities),
            checkedAtUtc = DateTimeOffset.UtcNow
        });
    }

    [HttpPost("prompt")]
    public async Task<IActionResult> TestPrompt([FromBody] PromptTestRequest request, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        if (request is null || string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 4000)
            return BadRequest(new { error = "Câu hỏi phải có từ 1 đến 4.000 ký tự." });

        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        using var upstream = await SendSignedAiAsync(HttpMethod.Post, "/api/ai/v1/rag/answer",
            JsonContent.Create(new { query = request.Query.Trim(), retrievalLimit = Math.Clamp(request.RetrievalLimit, 1, 10) }),
            actor, ct, "FvnAiChat");
        return await RelayAsync(upstream, ct);
    }

    [HttpPost("documents")]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument([FromForm] IFormFile file, [FromForm] string? moduleCode,
        [FromForm] string? schemaCode, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        if (file is null || file.Length <= 0) return BadRequest(new { error = "Vui lòng chọn tệp không rỗng." });
        var maxBytes = (long)Math.Clamp(_configuration.GetValue<int>("AIPlatform:MaximumFileSizeMb", 20), 1, 25) * 1024 * 1024;
        if (file.Length > maxBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge,
            new { error = $"Tệp vượt giới hạn {maxBytes / 1024 / 1024} MB." });

        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        using var form = new MultipartFormDataContent();
        await using var input = file.OpenReadStream();
        using var fileContent = new StreamContent(input);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        form.Add(fileContent, "file", System.IO.Path.GetFileName(file.FileName));
        if (!string.IsNullOrWhiteSpace(moduleCode)) form.Add(new StringContent(moduleCode.Trim()), "moduleCode");
        if (!string.IsNullOrWhiteSpace(schemaCode)) form.Add(new StringContent(schemaCode.Trim()), "schemaCode");

        using var upstream = await SendSignedAiAsync(HttpMethod.Post, "/api/ai/v1/documents", form, actor, ct);
        return await RelayAsync(upstream, ct);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetRecentJobs([FromQuery] int limit = 100, CancellationToken ct = default)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        var boundedLimit = Math.Clamp(limit, 1, 200);
        using var upstream = await SendSignedAiAsync(HttpMethod.Get,
            $"/api/ai/v1/operations/jobs?limit={boundedLimit}", null, actor, ct);
        return await RelayAsync(upstream, ct);
    }

    [HttpGet("documents/{documentId:guid}")]
    public async Task<IActionResult> GetDocumentStatus(Guid documentId, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        using var upstream = await SendSignedAiAsync(HttpMethod.Get, $"/api/ai/v1/documents/{documentId:D}", null, actor, ct);
        return await RelayAsync(upstream, ct);
    }

    [HttpGet("documents/{documentId:guid}/extraction")]
    public async Task<IActionResult> GetExtraction(Guid documentId, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        using var upstream = await SendSignedAiAsync(HttpMethod.Get, $"/api/ai/v1/documents/{documentId:D}/extraction", null, actor, ct);
        return await RelayAsync(upstream, ct);
    }

    /// <summary>Rejects the latest extraction of a document (also frees the file hash so it can be uploaded again).</summary>
    [HttpPost("documents/{documentId:guid}/reject")]
    public async Task<IActionResult> RejectDocument(Guid documentId, [FromBody] DocumentReviewTestRequest? request, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        var extractionId = await ResolveLatestExtractionIdAsync(documentId, actor, ct);
        if (extractionId is null) return NotFound(new { error = "Chưa có kết quả extraction để từ chối." });
        using var upstream = await SendSignedAiAsync(HttpMethod.Post, $"/api/ai/v1/reviews/{extractionId.Value:D}",
            JsonContent.Create(new { decision = "Rejected", correctedPayloadJson = (string?)null, comment = Truncate(request?.Comment) }),
            actor, ct);
        return await RelayAsync(upstream, ct);
    }

    /// <summary>
    /// Approves the latest extraction and publishes the document to the knowledge base. Publishing embeds every
    /// chunk and can take minutes, so the work runs in the background and the page polls publish-status.
    /// The AI API still enforces the reviewer/publisher right (function 3502, data scope All) on both calls.
    /// </summary>
    [HttpPost("documents/{documentId:guid}/approve-publish")]
    public async Task<IActionResult> ApproveAndPublish(Guid documentId, [FromBody] DocumentReviewTestRequest? request, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture);
        PruneFinishedPublishStates();
        if (PublishStates.TryGetValue(documentId, out var existing) && existing.Status == "Running")
            return Accepted(new { status = existing.Status, startedAtUtc = existing.StartedAtUtc });

        var extractionId = await ResolveLatestExtractionIdAsync(documentId, actor, ct);
        if (extractionId is null) return NotFound(new { error = "Chưa có kết quả extraction để duyệt." });

        var started = new PublishState("Running", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        PublishStates[documentId] = started;
        var comment = Truncate(request?.Comment);
        _ = Task.Run(() => RunApproveAndPublishAsync(documentId, extractionId.Value, actor, comment, started.StartedAtUtc));
        return Accepted(new { status = started.Status, startedAtUtc = started.StartedAtUtc });
    }

    /// <summary>Progress of the background approve-and-publish started from this page.</summary>
    [HttpGet("documents/{documentId:guid}/publish-status")]
    public async Task<IActionResult> GetPublishStatus(Guid documentId, CancellationToken ct)
    {
        if (!await CanManageDiagnosticsAsync(ct)) return Forbid();
        return PublishStates.TryGetValue(documentId, out var state)
            ? Ok(new { status = state.Status, startedAtUtc = state.StartedAtUtc, updatedAtUtc = state.UpdatedAtUtc, result = state.Result })
            : Ok(new { status = "None" });
    }

    private async Task RunApproveAndPublishAsync(Guid documentId, Guid extractionId, string actor, string? comment, DateTimeOffset startedAtUtc)
    {
        using var overall = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var ct = overall.Token;
        try
        {
            using var review = await SendSignedAiAsync(HttpMethod.Post, $"/api/ai/v1/reviews/{extractionId:D}",
                JsonContent.Create(new { decision = "Approved", correctedPayloadJson = (string?)null, comment }), actor, ct, "FvnAiLong");
            var reviewBody = await review.Content.ReadAsStringAsync(ct);
            if (!review.IsSuccessStatusCode)
            {
                FinishPublish(documentId, startedAtUtc, "Failed", new { stage = "review", error = $"Duyệt thất bại (HTTP {(int)review.StatusCode}).", detail = TryParseJson(reviewBody) });
                return;
            }
            var reviewJson = TryParseJson(reviewBody);
            if (reviewJson is not JsonElement reviewElement || !TryReadGuidProperty(reviewElement, "reviewId", out var reviewId))
            {
                FinishPublish(documentId, startedAtUtc, "Failed", new { stage = "review", error = "AI API không trả về reviewId.", detail = reviewJson });
                return;
            }

            using var publish = await SendSignedAiAsync(HttpMethod.Post, $"/api/ai/v1/knowledge/{documentId:D}/publish",
                JsonContent.Create(new { reviewId }), actor, ct, "FvnAiLong");
            var publishBody = await publish.Content.ReadAsStringAsync(ct);
            if (!publish.IsSuccessStatusCode)
            {
                FinishPublish(documentId, startedAtUtc, "Failed", new
                {
                    stage = "publish",
                    error = $"Đã duyệt nhưng publish thất bại (HTTP {(int)publish.StatusCode}). Kiểm tra quyền 3502 (data scope All), Ollama embedding và log AI API.",
                    review = reviewJson,
                    detail = TryParseJson(publishBody)
                });
                return;
            }
            FinishPublish(documentId, startedAtUtc, "Succeeded", new { stage = "published", review = reviewJson, publish = TryParseJson(publishBody) });
        }
        catch (OperationCanceledException)
        {
            FinishPublish(documentId, startedAtUtc, "Failed", new { stage = "publish", error = "Quá 20 phút chưa hoàn tất. Kiểm tra Ollama embedding và log AI API." });
        }
        catch (Exception ex)
        {
            FinishPublish(documentId, startedAtUtc, "Failed", new { stage = "publish", error = $"Lỗi không mong đợi: {ex.GetType().Name}" });
        }
    }

    private static void FinishPublish(Guid documentId, DateTimeOffset startedAtUtc, string status, object result) =>
        PublishStates[documentId] = new PublishState(status, startedAtUtc, DateTimeOffset.UtcNow, result);

    private static void PruneFinishedPublishStates()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        foreach (var pair in PublishStates)
            if (pair.Value.Status != "Running" && pair.Value.UpdatedAtUtc < cutoff) PublishStates.TryRemove(pair.Key, out _);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, PublishState> PublishStates = new();

    private sealed record PublishState(string Status, DateTimeOffset StartedAtUtc, DateTimeOffset UpdatedAtUtc, object? Result);

    private async Task<Guid?> ResolveLatestExtractionIdAsync(Guid documentId, string actor, CancellationToken ct)
    {
        using var upstream = await SendSignedAiAsync(HttpMethod.Get, $"/api/ai/v1/documents/{documentId:D}/extraction", null, actor, ct);
        if (!upstream.IsSuccessStatusCode) return null;
        var body = await upstream.Content.ReadAsStringAsync(ct);
        return TryParseJson(body) is JsonElement element && TryReadGuidProperty(element, "extractionId", out var id) ? id : null;
    }

    private static bool TryReadGuidProperty(JsonElement element, string name, out Guid value)
    {
        value = Guid.Empty;
        if (element.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String && Guid.TryParse(property.Value.GetString(), out value))
                return true;
        return false;
    }

    private static string? Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : (value.Trim().Length > 500 ? value.Trim()[..500] : value.Trim());

    private async Task<bool> CanManageDiagnosticsAsync(CancellationToken ct)
    {
        var user = UserInfo;
        return user is not null &&
            await _authorization.CanAccessAsync(user, SecurityFunctionCodes.SecurityView, null, null, ct);
    }

    // /health/ready and /api/ai/capabilities now require the shared service key (held server-side only).
    private async Task<HttpResponseMessage> SendServiceHealthAsync(Uri uri, CancellationToken ct)
    {
        var apiKey = _configuration["AIPlatform:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { status = "not-ready", error = "Cần cấu hình AIPlatform:ApiKey." }) };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
            return await _clients.CreateClient("FvnAi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { status = "unreachable", error = "Không kết nối được AI API." }) };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { status = "timeout", error = "AI API quá thời gian phản hồi." }) };
        }
    }

    private async Task<HttpResponseMessage> SendSignedAiAsync(HttpMethod method, string path, HttpContent? content, string actor, CancellationToken ct, string clientName = "FvnAi")
    {
        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"] ?? "FCCVN";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { error = "Cần cấu hình AIPlatform:BaseUrl, ApiKey và ContextSigningKey (tối thiểu 32 ký tự)." }) };

        byte[] bodyBytes = content is null ? Array.Empty<byte>() : await content.ReadAsByteArrayAsync(ct);
        var bodyHash = Convert.ToHexString(SHA256.HashData(bodyBytes));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var canonical = string.Join("\n", method.Method.ToUpperInvariant(), path, tenant, actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        using var request = new HttpRequestMessage(method, new Uri(baseUri, path.TrimStart('/')));
        if (content is not null)
        {
            var copy = new ByteArrayContent(bodyBytes);
            foreach (var header in content.Headers)
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = copy;
        }
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);

        try { return await _clients.CreateClient(clientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (HttpRequestException)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { error = "Không kết nối được AI API." }) };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { error = "AI API quá thời gian phản hồi." }) };
        }
    }

    private static async Task<IActionResult> RelayAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var payload = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = payload
        };
    }

    private static object? TryParseJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try { return JsonDocument.Parse(payload).RootElement.Clone(); }
        catch (JsonException) { return payload; }
    }
}

public sealed record PromptTestRequest(string Query, int RetrievalLimit = 3);
public sealed record DocumentReviewTestRequest(string? Comment = null);
