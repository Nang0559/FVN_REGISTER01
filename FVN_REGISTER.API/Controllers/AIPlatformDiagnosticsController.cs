using System.Globalization;
using System.Net;
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

        using var ready = await SendUnauthenticatedHealthAsync(new Uri(baseUri, "/health/ready"), ct);
        var readyBody = await ready.Content.ReadAsStringAsync(ct);
        string capabilities = "";
        try
        {
            using var caps = await SendUnauthenticatedHealthAsync(new Uri(baseUri, "/api/ai/capabilities"), ct);
            capabilities = await caps.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException) { }
        return Ok(new
        {
            reachable = true,
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
            actor, ct);
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
        form.Add(fileContent, "file", Path.GetFileName(file.FileName));
        if (!string.IsNullOrWhiteSpace(moduleCode)) form.Add(new StringContent(moduleCode.Trim()), "moduleCode");
        if (!string.IsNullOrWhiteSpace(schemaCode)) form.Add(new StringContent(schemaCode.Trim()), "schemaCode");

        using var upstream = await SendSignedAiAsync(HttpMethod.Post, "/api/ai/v1/documents", form, actor, ct);
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

    private async Task<bool> CanManageDiagnosticsAsync(CancellationToken ct)
    {
        var user = UserInfo;
        return user is not null &&
            await _authorization.CanAccessAsync(user, SecurityFunctionCodes.SecurityView, null, null, ct);
    }

    private async Task<HttpResponseMessage> SendUnauthenticatedHealthAsync(Uri uri, CancellationToken ct)
    {
        try { return await _clients.CreateClient("FvnAi").GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct); }
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

    private async Task<HttpResponseMessage> SendSignedAiAsync(HttpMethod method, string path, HttpContent? content, string actor, CancellationToken ct)
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

        try { return await _clients.CreateClient("FvnAi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
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
