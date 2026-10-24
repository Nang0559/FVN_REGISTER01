using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Users;
using FvnAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Read-only, authenticated projection of the current FVN_REGISTER identity and effective
/// permissions for AI context construction. This endpoint never accepts an actor id from
/// the request and never grants authority to the AI caller.
/// </summary>
[Authorize]
[ApiController]
[EnableRateLimiting("AIAuthenticated")]
[Route("api/ai/business-context")]
public sealed class AIBusinessContextController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly FvnAuthorizationService _authorization;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AIBusinessContextController(
        ICurrentUserService currentUser,
        FvnAuthorizationService authorization,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }


    /// <summary>
    /// Authenticated FVN-to-AI handoff. The actor is derived from the current FVN session;
    /// the AI API accepts this request only after validating the gateway HMAC and replay nonce.
    /// </summary>
    [HttpGet("ai")]
    public Task<IActionResult> GetAiContext(CancellationToken cancellationToken) =>
        PostAiContext(cancellationToken);

    /// <summary>
    /// Builds the authoritative context from the active FVN session and forwards the exact
    /// serialized snapshot over a content-bound HMAC channel to the private AI API.
    /// </summary>
    [HttpPost("ai")]
    public async Task<IActionResult> PostAiContext(CancellationToken cancellationToken)
    {
        var snapshotResult = await GetCurrentContext(cancellationToken);
        if (snapshotResult is not OkObjectResult ok || ok.Value is null)
            return snapshotResult;

        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"]?.Trim();
        const string path = "/api/ai/v1/business-context/snapshot";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 ||
            string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64)
            return Problem("AIPlatform BaseUrl, ApiKey, ContextSigningKey and TenantCode must be configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0)
            return Unauthorized();

        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var body = JsonSerializer.SerializeToUtf8Bytes(ok.Value);
        var bodyHash = Convert.ToHexString(SHA256.HashData(body));
        var canonical = string.Join("\n", "POST", path, tenant, actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, path.TrimStart('/')))
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "Authorization";
        try
        {
            using var upstream = await _httpClientFactory.CreateClient("FvnAi")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var payload = await upstream.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)upstream.StatusCode,
                ContentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = payload
            };
        }
        catch (HttpRequestException)
        {
            return Problem("AI service unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem("AI service timed out.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Ask the AI using the current authenticated FVN context in the same signed request.</summary>
    [HttpPost("ask")]
    public async Task<IActionResult> AskAi([FromBody] AssistantAskGatewayRequest input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Trim().Length > 4000)
            return BadRequest(new { error = "Question must contain 1 to 4000 characters." });
        var assistantAccess = await RequireAssistantAccessAsync(cancellationToken);
        if (assistantAccess is not null) return assistantAccess;

        var snapshotResult = await GetCurrentContext(cancellationToken);
        if (snapshotResult is not OkObjectResult ok || ok.Value is null)
            return snapshotResult;

        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"]?.Trim();
        const string path = "/api/ai/v1/assistant/ask";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 ||
            string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64)
            return Problem("AIPlatform BaseUrl, ApiKey, ContextSigningKey and TenantCode must be configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0) return Unauthorized();
        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        using var snapshotDocument = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        if (!snapshotDocument.RootElement.TryGetProperty("context", out var businessContext) ||
            businessContext.ValueKind != JsonValueKind.Object)
            return Problem("Authenticated FVN business context could not be constructed.", statusCode: 503);

        // Minimize personal data: the employee code and the full host response are not sent to the model.
        static string? ReadContextString(JsonElement source, string name)
        {
            return source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        static string[] ReadContextCodes(JsonElement source, string name)
        {
            if (!source.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Array)
                return [];
            return values.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value) && value!.Length <= 160)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToArray()!;
        }
        var safeBusinessContext = new
        {
            tenantCode = ReadContextString(businessContext, "tenantCode"),
            actorId = ReadContextString(businessContext, "actorId"),
            departmentCode = ReadContextString(businessContext, "departmentCode"),
            positionCode = ReadContextString(businessContext, "positionCode"),
            roleCodes = ReadContextCodes(businessContext, "roleCodes"),
            permissionCodes = ReadContextCodes(businessContext, "permissionCodes"),
            functionScopeCodes = ReadContextCodes(businessContext, "functionScopeCodes"),
            managedScopeCodes = ReadContextCodes(businessContext, "managedScopeCodes"),
            contextVersion = ReadContextString(businessContext, "contextVersion")
        };
        if (string.IsNullOrWhiteSpace(safeBusinessContext.tenantCode) ||
            string.IsNullOrWhiteSpace(safeBusinessContext.actorId) ||
            string.IsNullOrWhiteSpace(safeBusinessContext.contextVersion))
            return Problem("Authenticated FVN business context is incomplete.", statusCode: 503);

        var envelope = new
        {
            query = input.Query.Trim(),
            retrievalLimit = Math.Clamp(input.RetrievalLimit, 1, 20),
            businessContext = safeBusinessContext
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var bodyHash = Convert.ToHexString(SHA256.HashData(body));
        var canonical = string.Join("\n", "POST", path, tenant, actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, path.TrimStart('/'))) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "Authorization";
        try
        {
            using var upstream = await _httpClientFactory.CreateClient("FvnAi")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var payload = await upstream.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)upstream.StatusCode,
                ContentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = payload
            };
        }
        catch (HttpRequestException)
        {
            return Problem("AI service unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem("AI service timed out.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("conversations")]
    public Task<IActionResult> CreateConversation([FromBody] CreateAiConversationGatewayRequest input, CancellationToken ct) =>
        ForwardConversationAsync(HttpMethod.Post, "/api/ai/v1/conversations", new { title = input.Title }, false, ct);

    [HttpGet("conversations")]
    public Task<IActionResult> ListConversations(CancellationToken ct) =>
        ForwardConversationAsync(HttpMethod.Get, "/api/ai/v1/conversations", null, false, ct);

    [HttpGet("conversations/{conversationId:guid}")]
    public Task<IActionResult> GetConversation(Guid conversationId, CancellationToken ct) =>
        ForwardConversationAsync(HttpMethod.Get, $"/api/ai/v1/conversations/{conversationId:D}", null, false, ct);

    [HttpPost("conversations/ask")]
    public Task<IActionResult> AskInConversation([FromBody] AskAiConversationGatewayRequest input, CancellationToken ct)
    {
        if (input.ConversationId == Guid.Empty || string.IsNullOrWhiteSpace(input.Query) || input.Query.Trim().Length > 4000)
            return Task.FromResult<IActionResult>(BadRequest(new { error = "Conversation ID and question of 1 to 4000 characters are required." }));
        return ForwardConversationAsync(HttpMethod.Post, "/api/ai/v1/conversations/ask", input, true, ct);
    }

    private async Task<IActionResult> ForwardConversationAsync(HttpMethod method, string path, object? payload,
        bool includeBusinessContext, CancellationToken cancellationToken)
    {
        var assistantAccess = await RequireAssistantAccessAsync(cancellationToken);
        if (assistantAccess is not null) return assistantAccess;
        var snapshotResult = await GetCurrentContext(cancellationToken);
        if (snapshotResult is not OkObjectResult ok || ok.Value is null) return snapshotResult;
        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"]?.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 ||
            string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64)
            return Problem("AIPlatform gateway configuration is incomplete.", statusCode:503);

        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0) return Unauthorized();
        object? requestPayload = payload;
        if (includeBusinessContext)
        {
            using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
            if (!snapshot.RootElement.TryGetProperty("context", out var context) || context.ValueKind != JsonValueKind.Object)
                return Problem("Authenticated FVN business context is incomplete.", statusCode:503);
            static string? ReadString(JsonElement source, string name) =>
                source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            static string[] ReadCodes(JsonElement source, string name)
            {
                if (!source.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Array) return [];
                return values.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x) && x!.Length <= 160)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(500).ToArray()!;
            }
            requestPayload = new
            {
                conversationId = ((AskAiConversationGatewayRequest)payload!).ConversationId,
                query = ((AskAiConversationGatewayRequest)payload!).Query.Trim(),
                retrievalLimit = Math.Clamp(((AskAiConversationGatewayRequest)payload!).RetrievalLimit, 1, 20),
                businessContext = new
                {
                    tenantCode = ReadString(context, "tenantCode"), actorId = ReadString(context, "actorId"),
                    departmentCode = ReadString(context, "departmentCode"), positionCode = ReadString(context, "positionCode"),
                    roleCodes = ReadCodes(context, "roleCodes"), permissionCodes = ReadCodes(context, "permissionCodes"),
                    functionScopeCodes = ReadCodes(context, "functionScopeCodes"), managedScopeCodes = ReadCodes(context, "managedScopeCodes"),
                    contextVersion = ReadString(context, "contextVersion")
                }
            };
        }

        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var body = requestPayload is null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(requestPayload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var bodyHash = Convert.ToHexString(SHA256.HashData(body));
        var canonical = string.Join("\n", method.Method.ToUpperInvariant(), path, tenant, actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path.TrimStart('/')));
        if (requestPayload is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "Authorization";
        try
        {
            using var upstream = await _httpClientFactory.CreateClient("FvnAi").SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return new ContentResult { StatusCode = (int)upstream.StatusCode,
                ContentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = await upstream.Content.ReadAsStringAsync(cancellationToken) };
        }
        catch (HttpRequestException) { return Problem("AI service unavailable.", statusCode:503); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Problem("AI service timed out.", statusCode:503); }
    }

    private async Task<IActionResult?> RequireAssistantAccessAsync(CancellationToken cancellationToken)
    {
        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0)
            return Unauthorized();

        var allowed = await _authorization.HasAsync(user, SecurityFunctionCodes.AIAssistantChat, cancellationToken);
        return allowed ? null : Forbid();
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentContext(CancellationToken cancellationToken)
    {
        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0)
            return Unauthorized();

        var tenantCode = _configuration["AIPlatform:TenantCode"]?.Trim();
        if (string.IsNullOrWhiteSpace(tenantCode) || tenantCode.Length > 64)
            return Problem("AIPlatform:TenantCode must be configured before business context is exposed.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        // Load effective permissions from the authorization service on every request.
        // Do not trust role/function/scope claims supplied by the model or browser.
        var snapshot = await _authorization.GetSnapshotAsync(user.UserId, cancellationToken);
        var managedScopes = await _authorization.GetManagedScopesAsync(user.UserId, cancellationToken);

        var permissionCodes = snapshot.Functions
            .Select(x => $"FVN_FUNCTION:{x.FunctionCode.ToString(CultureInfo.InvariantCulture)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scopeCodes = snapshot.Functions
            .Where(x => !string.IsNullOrWhiteSpace(x.ScopeCode))
            .Select(x => $"{x.FunctionCode.ToString(CultureInfo.InvariantCulture)}:{x.ScopeCode!.Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Encode only stable scope selectors; never send remarks or employee lists to the model.
        // Keep the encoding deterministic so contextVersion changes when a managed boundary changes.
        var managedScopeCodes = managedScopes
            .Select(scope => string.Join("|",
                scope.NodeType ?? string.Empty,
                scope.NodeCode ?? string.Empty,
                scope.FactoryCode ?? string.Empty,
                Convert.ToString(scope.DeptCode, CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(scope.SubDepartmentCode, CultureInfo.InvariantCulture) ?? string.Empty,
                scope.IncludeChildren ? "1" : "0"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var managedScopeSnapshot = JsonSerializer.Serialize(managedScopeCodes);
        var contextVersionSource = string.Join("|",
            tenantCode,
            user.UserId.ToString(CultureInfo.InvariantCulture),
            user.EmployeeCode ?? string.Empty,
            user.DeptCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            user.PositionCode ?? string.Empty,
            string.Join(",", snapshot.RoleCodes.OrderBy(x => x)),
            string.Join(",", permissionCodes),
            string.Join(",", scopeCodes),
            managedScopeSnapshot);
        var contextVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contextVersionSource)));

        var functions = snapshot.Functions
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.FunctionCode)
            .Select(x => new
            {
                functionCode = x.FunctionCode,
                functionName = x.FunctionName,
                description = x.Detail,
                moduleCode = x.ModuleCode,
                actionCode = x.ActionCode,
                scopeCode = x.ScopeCode,
                accessMode = x.AccessMode,
                isSystemCritical = x.IsSystemCritical
            })
            .ToArray();

        // Group only authoritative permission metadata. This is discovery metadata, not an
        // executable tool catalog and must never be treated as a grant by the AI service.
        var modules = functions
            .Where(x => !string.IsNullOrWhiteSpace(x.moduleCode))
            .GroupBy(x => x.moduleCode!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                moduleCode = group.Key,
                displayName = group.Key,
                functions = group.Select(x => new
                {
                    functionCode = x.functionCode,
                    functionName = x.functionName,
                    description = x.description,
                    actionCode = x.actionCode,
                    scopeCode = x.scopeCode,
                    accessMode = x.accessMode,
                    isSystemCritical = x.isSystemCritical
                }).ToArray()
            })
            .ToArray();

        // Permission snapshots contain security-sensitive data. Never let a proxy/browser
        // cache a previous user's context and replay it after an account switch.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "Authorization";

        return Ok(new
        {
            context = new
            {
                tenantCode,
                actorId = user.UserId.ToString(CultureInfo.InvariantCulture),
                employeeCode = user.EmployeeCode,
                departmentCode = user.DeptCode?.ToString(CultureInfo.InvariantCulture),
                positionCode = user.PositionCode,
                roleCodes = snapshot.RoleCodes.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray(),
                permissionCodes,
                functionScopeCodes = scopeCodes,
                managedScopeCodes,
                retrievedAtUtc = DateTimeOffset.UtcNow,
                contextVersion
            },
            modules,
            functions,
            managedScopeCodes
        });
    }
}

public sealed record AssistantAskGatewayRequest(string Query, int RetrievalLimit = 8);

public sealed record CreateAiConversationGatewayRequest(string? Title);
public sealed record AskAiConversationGatewayRequest(Guid ConversationId, string Query, int RetrievalLimit = 8);
