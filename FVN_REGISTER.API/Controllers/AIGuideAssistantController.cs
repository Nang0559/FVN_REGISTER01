using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FVN_REGISTER.API.Services;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Help;
using FVN_REGISTER.Contract.Help;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FvnAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Question answering over the Markdown user guide (the same content as <c>/huong-dan</c>).
/// The server picks the guide passages: only passages the caller may see under <see cref="HelpAccessPolicy"/> are candidates, and the
/// browser can neither add passages nor widen access. The selected passages are sent to the private AI API over the same
/// body-bound HMAC channel as the other AI gateway routes.
/// </summary>
[Authorize]
[ApiController]
[EnableRateLimiting("AIAuthenticated")]
[Route("api/ai/guide")]
public sealed class AIGuideAssistantController : ControllerBase
{
    private const int MaxQueryChars = 1_000;
    private const string AiPath = "/api/ai/v1/guide/answer";

    private static readonly Regex FeatureCodePattern = new("^[A-Z0-9_]{1,64}$", RegexOptions.Compiled);

    // A CPU-only local model answers one or two questions at a time; more would only make everyone slower.
    private static readonly SemaphoreSlim ModelSlots = new(2, 2);

    private readonly ICurrentUserService _currentUser;
    private readonly FvnAuthorizationService _authorization;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGuideKnowledgeIndex _index;
    private readonly ILogger<AIGuideAssistantController> _logger;

    public AIGuideAssistantController(
        ICurrentUserService currentUser,
        FvnAuthorizationService authorization,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IGuideKnowledgeIndex index,
        ILogger<AIGuideAssistantController> logger)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _index = index;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] GuideAskRequestDto input, CancellationToken cancellationToken)
    {
        var query = input.Query?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaxQueryChars)
            return BadRequest(new { message = $"Câu hỏi phải có từ 1 đến {MaxQueryChars} ký tự." });

        var user = _currentUser.GetCurrentUser();
        if (user is null || user.UserId <= 0) return Unauthorized(new { message = "Không xác định được tài khoản đang đăng nhập. Vui lòng đăng nhập lại." });
        if (!await _authorization.HasAsync(user, SecurityFunctionCodes.AIAssistantChat, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tài khoản chưa được cấp quyền dùng trợ lý AI (AIAssistant.Chat)." });

        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"]?.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 || string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ AI chưa được cấu hình." });

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "Authorization";

        // 1) Retrieval is local and fast; when nothing matches, the model is not called at all.
        var chunks = await _index.GetChunksAsync(cancellationToken);
        if (chunks.Count == 0)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Chưa tìm thấy bộ hướng dẫn (thư mục Guide) trên máy chủ API." });

        var snapshot = await _authorization.GetSnapshotAsync(user.UserId, cancellationToken);
        bool CanSee(GuideChunk chunk) => chunk.General || chunk.FeatureCodes.Any(code => HelpAccessPolicy.CanView(snapshot, code));

        var feature = input.Feature?.Trim().ToUpperInvariant();
        if (feature is null || !FeatureCodePattern.IsMatch(feature)) feature = null;

        var history = (input.History ?? new List<GuideChatTurnDto>())
            .Where(t => t is not null && t.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(t.Content))
            .TakeLast(4)
            .Select(t => new { role = t.Role, content = t.Content.Length > 500 ? t.Content[..500] : t.Content })
            .ToArray();

        // A very short follow-up ("còn OT thì sao?", "bước tiếp theo?") is searched together with the previous question.
        var retrievalQuery = query;
        if (GuideSearch.Tokens(query).Count < 3)
        {
            var previous = history.LastOrDefault(t => t.role == "user");
            if (previous is not null) retrievalQuery = previous.content + " " + query;
        }

        var maxPassages = Math.Clamp(_configuration.GetValue("AIPlatform:Guide:MaxPassages", 4), 1, 6);
        var maxChars = Math.Clamp(_configuration.GetValue("AIPlatform:Guide:MaxEvidenceChars", 3600), 800, 10_000);
        var hits = GuideSearch.Search(chunks, retrievalQuery, CanSee, feature, maxPassages, maxChars);
        // Approved knowledge-base documents are searched by the AI API with the user's own document permissions.
        // Turn off with AIPlatform:Guide:IncludeApprovedDocuments=false.
        var includeDocuments = _configuration.GetValue("AIPlatform:Guide:IncludeApprovedDocuments", true);
        if (hits.Count == 0 && !includeDocuments)
            return Ok(new GuideAnswerDto
            {
                Status = "NotFound",
                Answer = "Tôi chưa tìm thấy nội dung phù hợp trong tài liệu hướng dẫn mà tài khoản của bạn được xem. " +
                         "Bạn thử hỏi cụ thể hơn (ví dụ: “Cách đăng ký OT”, “Duyệt đơn nghỉ phép ở đâu?”) hoặc mở mục hướng dẫn của chức năng bạn đang dùng."
            });

        var evidence = hits.Select((hit, i) => new { id = $"S{i + 1}", title = Label(hit.Chunk), text = hit.Chunk.Text }).ToArray();
        var envelope = new { query, searchQuery = retrievalQuery, feature, history, evidence, includeDocuments };

        // 2) Signed call to the private AI API (same canonical string as the other gateway routes).
        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var bodyHash = Convert.ToHexString(SHA256.HashData(body));
        var canonical = string.Join("\n", "POST", AiPath, tenant, actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, AiPath.TrimStart('/'))) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);

        if (!await ModelSlots.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Trợ lý AI đang bận, vui lòng thử lại sau ít giây." });
        try
        {
            using var upstream = await _httpClientFactory.CreateClient("FvnAiChat").SendAsync(request, cancellationToken);
            var payload = await upstream.Content.ReadAsStringAsync(cancellationToken);
            if (!upstream.IsSuccessStatusCode)
            {
                _logger.LogWarning("Guide assistant: AI API returned {Status}.", (int)upstream.StatusCode);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau." });
            }

            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            var answer = new GuideAnswerDto
            {
                Answer = root.TryGetProperty("answer", out var a) ? a.GetString() ?? string.Empty : string.Empty,
                Status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "NotFound" : "NotFound",
                Model = root.TryGetProperty("modelVersion", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null,
                ElapsedMilliseconds = root.TryGetProperty("elapsedMilliseconds", out var e) && e.TryGetInt64(out var ms) ? ms : 0
            };
            if (answer.Status == "NotFound" || string.IsNullOrWhiteSpace(answer.Answer))
            {
                answer.Status = "NotFound";
                answer.Answer = "Tài liệu hướng dẫn và các tài liệu đã phê duyệt mà bạn được xem chưa nói rõ về câu hỏi này. Bạn thử diễn đạt cụ thể hơn hoặc xem mục hướng dẫn của chức năng liên quan.";
                return Ok(answer);
            }

            var cited = root.TryGetProperty("citedEvidenceIds", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < hits.Count; i++)
            {
                if (cited.Count > 0 && !cited.Contains($"S{i + 1}")) continue;
                var chunk = hits[i].Chunk;
                if (answer.Sources.Any(x => x.Title == Label(chunk))) continue;
                answer.Sources.Add(new GuideSourceDto { Title = Label(chunk), Feature = PickFeature(chunk, feature, snapshot) });
            }
            if (root.TryGetProperty("documentSources", out var documents) && documents.ValueKind == JsonValueKind.Array)
            {
                foreach (var document in documents.EnumerateArray().Take(3))
                {
                    var title = document.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                    var documentId = document.TryGetProperty("documentId", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    answer.Sources.Add(new GuideSourceDto { Kind = "Document", Title = title!, DocumentId = documentId });
                }
            }
            return Ok(answer);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không kết nối được dịch vụ AI." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "Trợ lý AI trả lời quá lâu. Vui lòng thử lại." });
        }
        catch (JsonException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Phản hồi của dịch vụ AI không hợp lệ." });
        }
        finally { ModelSlots.Release(); }
    }

    private static string Label(GuideChunk chunk) => chunk.SourceFile switch
    {
        "06_FAQ.md" => "Câu hỏi thường gặp › " + chunk.Title,
        "05_QUICK_GUIDE.md" => "Hướng dẫn nhanh › " + chunk.Title,
        _ => chunk.Title
    };

    /// <summary>The page to open for a source: the screen the user is on when it documents the passage, else the first one the user may view.</summary>
    private static string? PickFeature(GuideChunk chunk, string? current, FVN_REGISTER.Contract.Dtos.Security.PermissionSnapshotDto snapshot)
    {
        if (current is not null && chunk.FeatureCodes.Contains(current, StringComparer.Ordinal) && HelpAccessPolicy.CanView(snapshot, current)) return current;
        return chunk.FeatureCodes.FirstOrDefault(code => HelpAccessPolicy.CanView(snapshot, code));
    }
}
