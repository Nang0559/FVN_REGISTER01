using System.Text.Json;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using FvnAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Lets the UI hide the "Trợ lý AI" menu when the AI service cannot answer questions.
/// It returns one boolean only. Dependency details stay behind the service key and are never
/// exposed to the browser; the AI service key is used server-side.
/// </summary>
[Authorize]
[ApiController]
[EnableRateLimiting("AIAuthenticated")]
[Route("api/ai/assistant")]
public sealed class AIAssistantAvailabilityController : ControllerBase
{
    private const string CacheKey = "ai-platform:rag-operational";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly ICurrentUserService _currentUser;
    private readonly FvnAuthorizationService _authorization;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _cache;

    public AIAssistantAvailabilityController(
        ICurrentUserService currentUser,
        FvnAuthorizationService authorization,
        IConfiguration configuration,
        IHttpClientFactory clients,
        IMemoryCache cache)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _configuration = configuration;
        _clients = clients;
        _cache = cache;
    }

    /// <summary>True only when the caller may chat and the AI service reports ragOperational = true.</summary>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(CancellationToken cancellationToken)
    {
        var user = _currentUser.GetCurrentUser();
        if (user is null || !user.IsLoggedIn || user.UserId <= 0) return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        if (!await _authorization.HasAsync(user, SecurityFunctionCodes.AIAssistantChat, cancellationToken))
            return Ok(ApiResponse<bool>.Ok(false));

        // The service state is global, so it is cached briefly instead of probing per page render.
        if (!_cache.TryGetValue(CacheKey, out bool operational))
        {
            operational = await ProbeRagOperationalAsync(cancellationToken);
            _cache.Set(CacheKey, operational, CacheDuration);
        }

        return Ok(ApiResponse<bool>.Ok(operational));
    }

    private async Task<bool> ProbeRagOperationalAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(apiKey))
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "/api/ai/capabilities"));
            request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
            using var response = await _clients.CreateClient("FvnAi")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return false;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            return json.RootElement.TryGetProperty("ragOperational", out var flag)
                && flag.ValueKind == JsonValueKind.True;
        }
        catch (HttpRequestException) { return false; }
        catch (JsonException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }
}
