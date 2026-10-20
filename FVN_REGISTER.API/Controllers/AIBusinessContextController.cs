using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Read-only, authenticated projection of the current FVN_REGISTER identity and effective
/// permissions for AI context construction. This endpoint never accepts an actor id from
/// the request and never grants authority to the AI caller.
/// </summary>
[Authorize]
[ApiController]
[Route("api/ai/business-context")]
public sealed class AIBusinessContextController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AIBusinessContextController(
        ICurrentUserService currentUser,
        IAuthorizationService authorization,
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
    public async Task<IActionResult> GetAiContext(CancellationToken cancellationToken)
    {
        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0)
            return Unauthorized();

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "Authorization";

        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"]?.Trim();
        const string path = "/api/ai/v1/business-context/me";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 ||
            string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64)
            return Problem("AIPlatform BaseUrl, ApiKey, ContextSigningKey and TenantCode must be configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var bodyHash = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()));
        var canonical = string.Join("\n", "GET", path, tenant, actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path.TrimStart('/')));
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);

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

        var managedScopeSnapshot = JsonSerializer.Serialize(managedScopes);
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
                retrievedAtUtc = DateTimeOffset.UtcNow,
                contextVersion
            },
            modules,
            functions,
            managedScopes
        });
    }
}
