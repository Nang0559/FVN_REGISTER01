using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/ai/document-data-entry")]
public sealed class AIDocumentDataEntryController : BaseApiController
{
    private readonly IAuthorizationService _authorization;
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;

    public AIDocumentDataEntryController(IAuthorizationService authorization, IHttpClientFactory clients, IConfiguration configuration,
        ICurrentUserService currentUser, IUserLogService userLog, ILogger<AIDocumentDataEntryController> logger,
        IOptionsMonitor<AuthDebugOptions> options) : base(currentUser, userLog, logger, options)
    { _authorization = authorization; _clients = clients; _configuration = configuration; }

    [HttpGet("templates")]
    public Task<IActionResult> ListTemplates(CancellationToken ct) => ExecuteAuthorizedAsync("GET", "/api/ai/v1/data-entry/templates", null, ct);

    [HttpPost("templates")]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> CreateTemplate([FromForm] IFormFile file, [FromForm] string templateCode, [FromForm] string displayName,
        [FromForm] string moduleCode, [FromForm] string schemaCode, [FromForm] string definitionJson, CancellationToken ct)
    {
        if (!await CanUseAsync(ct)) return Forbid();
        if (file is null || file.Length <= 0) return BadRequest(new { error = "Vui lòng chọn file Excel mẫu." });
        if (string.IsNullOrWhiteSpace(definitionJson)) return BadRequest(new { error = "Thiếu định nghĩa mapping Excel." });
        try { JsonSerializer.Deserialize<DataEntryTemplateDefinition>(definitionJson); } catch (JsonException) { return BadRequest(new { error = "Mapping Excel không phải JSON hợp lệ." }); }
        using var form = new MultipartFormDataContent();
        await using var input = file.OpenReadStream(); using var content = new StreamContent(input);
        content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : file.ContentType);
        form.Add(content, "file", Path.GetFileName(file.FileName)); form.Add(new StringContent(templateCode ?? ""), "templateCode"); form.Add(new StringContent(displayName ?? ""), "displayName"); form.Add(new StringContent(moduleCode ?? ""), "moduleCode"); form.Add(new StringContent(schemaCode ?? ""), "schemaCode"); form.Add(new StringContent(definitionJson), "definitionJson");
        return await ExecuteAuthorizedAsync("POST", "/api/ai/v1/data-entry/templates", form, ct);
    }

    [HttpPost("runs")]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> CreateRun([FromForm] IFormFile file, [FromForm] string templateCode, CancellationToken ct)
    {
        if (!await CanUseAsync(ct)) return Forbid();
        if (file is null || file.Length <= 0) return BadRequest(new { error = "Vui lòng chọn giấy tờ cần nhập." });
        using var form = new MultipartFormDataContent(); await using var input = file.OpenReadStream(); using var content = new StreamContent(input);
        content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        form.Add(content, "file", Path.GetFileName(file.FileName)); form.Add(new StringContent(templateCode ?? ""), "templateCode");
        return await ExecuteAuthorizedAsync("POST", "/api/ai/v1/data-entry/runs", form, ct);
    }

    [HttpGet("runs/{runId:guid}")]
    public Task<IActionResult> GetRun(Guid runId, CancellationToken ct) => ExecuteAuthorizedAsync("GET", $"/api/ai/v1/data-entry/runs/{runId:D}", null, ct);

    [HttpPost("runs/{runId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid runId, [FromBody] ConfirmRequest request, CancellationToken ct)
    {
        if (!await CanUseAsync(ct)) return Forbid();
        var content = JsonContent.Create(request);
        return await ExecuteAuthorizedAsync("POST", $"/api/ai/v1/data-entry/runs/{runId:D}/confirm", content, ct);
    }

    [HttpGet("runs/{runId:guid}/export")]
    public async Task<IActionResult> Export(Guid runId, CancellationToken ct)
    {
        if (!await CanUseAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture); using var response = await SendSignedAiAsync(HttpMethod.Get, $"/api/ai/v1/data-entry/runs/{runId:D}/export", null, actor, ct);
        if (!response.IsSuccessStatusCode) return await RelayAsync(response, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct); var fileName = response.Content.Headers.ContentDisposition?.FileNameStar?.Trim('"') ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"data-entry-{runId:N}.xlsx";
        return File(bytes, response.Content.Headers.ContentType?.MediaType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task<IActionResult> ExecuteAuthorizedAsync(string method, string path, HttpContent? content, CancellationToken ct)
    {
        if (!await CanUseAsync(ct)) return Forbid();
        var actor = UserInfo!.UserId.ToString(CultureInfo.InvariantCulture); using var response = await SendSignedAiAsync(new HttpMethod(method), path, content, actor, ct); return await RelayAsync(response, ct);
    }

    private async Task<bool> CanUseAsync(CancellationToken ct)
    {
        var user = UserInfo; return user is not null && await _authorization.CanAccessAsync(user, SecurityFunctionCodes.SecurityView, null, null, ct);
    }

    private async Task<HttpResponseMessage> SendSignedAiAsync(HttpMethod method, string path, HttpContent? content, string actor, CancellationToken ct)
    {
        var baseUrl = _configuration["AIPlatform:BaseUrl"]; var apiKey = _configuration["AIPlatform:ApiKey"]; var signingKey = _configuration["AIPlatform:ContextSigningKey"]; var tenant = _configuration["AIPlatform:TenantCode"] ?? "FCCVN";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "Cần cấu hình AIPlatform:BaseUrl, ApiKey và ContextSigningKey (tối thiểu 32 ký tự)." }) };
        var bodyBytes = content is null ? Array.Empty<byte>() : await content.ReadAsByteArrayAsync(ct); var bodyHash = Convert.ToHexString(SHA256.HashData(bodyBytes)); var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture); var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(); var canonical = string.Join("\n", method.Method.ToUpperInvariant(), path, tenant, actor, timestamp, nonce, bodyHash); var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path.TrimStart('/')));
        if (content is not null) { var copy = new ByteArrayContent(bodyBytes); foreach (var header in content.Headers) copy.Headers.TryAddWithoutValidation(header.Key, header.Value); request.Content = copy; }
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey); request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant); request.Headers.TryAddWithoutValidation("X-Actor-Id", actor); request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp); request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce); request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);
        try { return await _clients.CreateClient("FvnAi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (HttpRequestException) { return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "Không kết nối được AI API." }) }; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "AI API quá thời gian phản hồi." }) }; }
    }

    private static async Task<IActionResult> RelayAsync(HttpResponseMessage response, CancellationToken ct)
    { var payload = await response.Content.ReadAsStringAsync(ct); return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json", Content = payload }; }

    public sealed record ConfirmRequest(string RowsJson, string? Comment = null);
}
