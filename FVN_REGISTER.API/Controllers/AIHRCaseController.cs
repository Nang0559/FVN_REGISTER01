using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Reports;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Reports;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Authenticated FVN_REGISTER gateway for AI HR cases. All operational facts are read through
/// the existing report dispatcher after function and employee managed-scope authorization.
/// The browser never receives the AI service key or context-signing secret.
/// </summary>
[Authorize]
[ApiController]
[Route("api/ai/hr-cases")]
public sealed class AIHRCaseController : BaseApiController
{
    private readonly IAuthorizationService _authorization;
    private readonly IReportDispatcher _reports;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AIHRCaseController(
        IAuthorizationService authorization,
        IReportDispatcher reports,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<AIHRCaseController> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _authorization = authorization;
        _reports = reports;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] HRCaseAnalysisRequest request, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.UserStatement) ||
            request.UserStatement.Length > 8000 || request.WorkDate == default)
            return BadRequest(new { error = "Nội dung phản hồi hoặc ngày làm việc không hợp lệ." });

        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? user.EmployeeCode : request.EmployeeCode.Trim();
        if (string.IsNullOrWhiteSpace(employeeCode))
            return BadRequest(new { error = "Không xác định được mã nhân viên." });

        var viewFunction = ViewFunction(request.CaseType);
        if (viewFunction == 0 || !await _authorization.CanAccessAsync(user, viewFunction, employeeCode, null, ct))
            return Forbid();

        var facts = await LoadOfficialFactsAsync(request.CaseType, employeeCode, request.WorkDate, user, ct);
        if (facts is null)
            return UnprocessableEntity(new { error = "Không tìm thấy dữ liệu HR chính thức đủ điều kiện đối chiếu cho nhân viên/ngày đã chọn." });

        var normalized = request with { EmployeeCode = employeeCode };
        return await ForwardAsync(HttpMethod.Post, "/api/ai/v1/hr-cases/analyze-authorized",
            new AuthorizedHRCaseAnalysisRequest(normalized, facts), user.UserId.ToString(CultureInfo.InvariantCulture), ct);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        using var upstream = await SendAiAsync(HttpMethod.Get, "/api/ai/v1/hr-cases/pending?limit=500", null,
            user.UserId.ToString(CultureInfo.InvariantCulture), ct);
        if (!upstream.IsSuccessStatusCode) return await RelayAsync(upstream);
        var items = await upstream.Content.ReadFromJsonAsync<List<HRCaseInboxItem>>(cancellationToken: ct) ?? new();
        var visible = new List<HRCaseInboxItem>();
        foreach (var item in items)
        {
            if (!Enum.TryParse<HRCaseType>(item.CaseType, out var caseType)) continue;
            var function = ReviewFunction(caseType);
            if (function != 0 && await _authorization.CanAccessAsync(user, function, item.EmployeeCode, null, ct))
                visible.Add(item);
            if (visible.Count >= Math.Clamp(limit, 1, 100)) break;
        }
        return Ok(visible);
    }

    [HttpGet("{caseId:guid}")]
    public async Task<IActionResult> GetCase(Guid caseId, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        using var upstream = await SendAiAsync(HttpMethod.Get, $"/api/ai/v1/hr-cases/{caseId:D}", null,
            user.UserId.ToString(CultureInfo.InvariantCulture), ct);
        if (upstream.StatusCode == HttpStatusCode.NotFound) return NotFound();
        if (!upstream.IsSuccessStatusCode) return await RelayAsync(upstream);
        var item = await upstream.Content.ReadFromJsonAsync<HRCaseInboxItem>(cancellationToken: ct);
        if (item is null || !Enum.TryParse<HRCaseType>(item.CaseType, out var type)) return NotFound();
        if (!await _authorization.CanAccessAsync(user, ReviewFunction(type), item.EmployeeCode, null, ct))
            return Forbid();
        return Ok(item);
    }

    [HttpPost("{caseId:guid}/review")]
    public async Task<IActionResult> Review(Guid caseId, [FromBody] HRCaseReviewRequest request, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        using var get = await SendAiAsync(HttpMethod.Get, $"/api/ai/v1/hr-cases/{caseId:D}", null, actor, ct);
        if (get.StatusCode == HttpStatusCode.NotFound) return NotFound();
        if (!get.IsSuccessStatusCode) return await RelayAsync(get);
        var item = await get.Content.ReadFromJsonAsync<HRCaseInboxItem>(cancellationToken: ct);
        if (item is null || !Enum.TryParse<HRCaseType>(item.CaseType, out var type)) return NotFound();
        if (!await _authorization.CanAccessAsync(user, ReviewFunction(type), item.EmployeeCode, null, ct))
            return Forbid();

        return await ForwardAsync(HttpMethod.Post, $"/api/ai/v1/hr-cases/{caseId:D}/review", request, actor, ct);
    }

    [HttpPost("evidence")]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> UploadEvidence(IFormFile file, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        if (file is null || file.Length <= 0) return BadRequest(new { error = "Chưa chọn tệp bằng chứng." });
        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var path = "/api/ai/v1/documents";
        using var form = new MultipartFormDataContent();
        await using var input = file.OpenReadStream();
        using var streamContent = new StreamContent(input);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        form.Add(streamContent, "file", Path.GetFileName(file.FileName));
        using var upstream = await SendAiAsync(HttpMethod.Post, path, form, actor, ct);
        return await RelayAsync(upstream);
    }

    [HttpPost("evidence/base64")]
    [RequestSizeLimit(28 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidenceBase64([FromBody] EvidenceUploadRequest upload, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        if (upload is null || string.IsNullOrWhiteSpace(upload.FileName) || upload.FileName.Length > 260 ||
            string.IsNullOrWhiteSpace(upload.ContentBase64))
            return BadRequest(new { error = "FileName and ContentBase64 are required." });
        byte[] bytes;
        try { bytes = Convert.FromBase64String(upload.ContentBase64); }
        catch (FormatException) { return BadRequest(new { error = "Evidence content is not valid base64." }); }
        var maxBytes = (long)Math.Clamp(_configuration.GetValue<int>("AIPlatform:MaximumFileSizeMb", 20), 1, 20) * 1024 * 1024;
        if (bytes.Length == 0 || bytes.LongLength > maxBytes)
            return BadRequest(new { error = $"Evidence file must be between 1 byte and {maxBytes / 1024 / 1024} MB." });

        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        var path = "/api/ai/v1/documents";
        using var form = new MultipartFormDataContent();
        using var stream = new MemoryStream(bytes, writable: false);
        using var streamContent = new StreamContent(stream);
        var mediaType = System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(upload.ContentType, out var parsedType)
            ? parsedType : new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        streamContent.Headers.ContentType = mediaType;
        form.Add(streamContent, "file", Path.GetFileName(upload.FileName));
        using var upstream = await SendAiAsync(HttpMethod.Post, path, form, actor, ct);
        return await RelayAsync(upstream);
    }

    [HttpGet("evidence/{documentId:guid}")]
    public async Task<IActionResult> GetEvidenceStatus(Guid documentId, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        using var upstream = await SendAiAsync(HttpMethod.Get,
            $"/api/ai/v1/documents/{documentId:D}", null, actor, ct);
        return await RelayAsync(upstream);
    }

    [HttpGet("evidence/{documentId:guid}/extraction")]
    public async Task<IActionResult> GetEvidenceExtraction(Guid documentId, CancellationToken ct)
    {
        var user = UserInfo;
        if (user is null) return Unauthorized();
        var actor = user.UserId.ToString(CultureInfo.InvariantCulture);
        using var upstream = await SendAiAsync(HttpMethod.Get,
            $"/api/ai/v1/documents/{documentId:D}/extraction", null, actor, ct);
        return await RelayAsync(upstream);
    }

    private async Task<OfficialHRFacts?> LoadOfficialFactsAsync(HRCaseType type, string employeeCode,
        DateOnly workDate, FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user, CancellationToken ct)
    {
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        if (type == HRCaseType.Overtime)
        {
            var detail = await GetReportAsync(ReportType.OTDetail, employeeCode, date, date, user, ct);
            var summary = await GetReportAsync(ReportType.OTSummaryByEmployee, employeeCode, date, date, user, ct);
            var attendance = await GetReportAsync(ReportType.AttendanceDetail, employeeCode, date, date, user, ct);
            var otRows = RowsForEmployee(detail, employeeCode);
            var summaryRow = RowsForEmployee(summary, employeeCode).FirstOrDefault();
            var attendanceRow = RowsForEmployee(attendance, employeeCode).FirstOrDefault();
            if (detail is null && summary is null && attendance is null) return null;

            var planned = Sum(otRows, "TotalOTHours");
            var actual = summaryRow is null || Number(summaryRow, "ActualRecordCount") is not > 0 ? null : Number(summaryRow, "TotalActual");
            var hasActualInterval = summaryRow is not null && Number(summaryRow, "ActualIntervalCount") is > 0;
            var plannedRow = otRows.Count == 1 ? otRows[0] : null;
            var plannedStart = plannedRow is null ? null : ToOffset(plannedRow, "StartTime", workDate);
            var plannedEnd = plannedRow is null ? null : ToOffset(plannedRow, "EndTime", workDate);
            var actualStart = hasActualInterval ? ToOffset(summaryRow!, "ActualStartTime", workDate) : null;
            var actualEnd = hasActualInterval ? ToOffset(summaryRow!, "ActualEndTime", workDate) : null;
            var attendanceOt = attendanceRow is null ? null : Number(attendanceRow, "OTHours");
            var statuses = string.Join(", ", otRows.Select(x => Text(x, "RequestStatus")).Where(x => x is not null).Distinct());
            var ids = otRows.Select(x => Text(x, "Id")).Where(x => x is not null).ToArray();
            var refs = string.Join(",", ids.Take(4)) + (ids.Length > 4 ? $",+{ids.Length - 4} more" : string.Empty);
            var requestStatus = string.IsNullOrWhiteSpace(statuses)
                ? (otRows.Count == 0 ? "No OT request found for selected employee/date" : null)
                : statuses;
            return new OfficialHRFacts(employeeCode, workDate, requestStatus,
                plannedStart, plannedEnd, planned, actualStart, actualEnd, actual, attendanceOt,
                attendanceRow is null ? null : Text(attendanceRow, "ShiftName"),
                attendanceRow is null ? null : Boolean(attendanceRow, "IsHoliday"),
                null, null, "FVN_REGISTER.ReportDispatcher",
                refs.Length == 0 ? $"OT:{employeeCode}:{workDate:yyyyMMdd}" : $"OT:{refs}",
                DateTimeOffset.UtcNow);
        }

        if (type == HRCaseType.Attendance)
        {
            var report = await GetReportAsync(ReportType.AttendanceDetail, employeeCode, date, date, user, ct);
            if (report is null) return null;
            var row = RowsForEmployee(report, employeeCode).FirstOrDefault();
            return new OfficialHRFacts(employeeCode, workDate, row is null ? "No attendance row found" : "AttendanceCalculated",
                null, null, null, row is null ? null : ToOffset(row, "CheckIn", workDate),
                row is null ? null : ToOffset(row, "CheckOut", workDate),
                row is null ? null : Number(row, "TotalHours"), row is null ? null : Number(row, "OTHours"),
                row is null ? null : Text(row, "ShiftName"), row is null ? null : Boolean(row, "IsHoliday"),
                null, null, "FVN_REGISTER.ReportDispatcher",
                $"Attendance:{employeeCode}:{workDate:yyyyMMdd}", DateTimeOffset.UtcNow);
        }

        var leave = await GetReportAsync(ReportType.LeaveDetail, employeeCode, date.AddYears(-1), date, user, ct);
        if (leave is null) return null;
        var leaveRow = leave.Rows.FirstOrDefault(x =>
        {
            var code = Text(x, "EmployeeCode");
            var start = DateValue(x, "StartDate");
            var end = DateValue(x, "EndDate");
            return string.Equals(code, employeeCode, StringComparison.OrdinalIgnoreCase) &&
                   start.HasValue && end.HasValue && start.Value.Date <= date.Date && end.Value.Date >= date.Date;
        });
        if (leaveRow is null)
            return new OfficialHRFacts(employeeCode, workDate, "No leave request found for selected employee/date",
                null, null, null, null, null, null, null, null, null, null, null,
                "FVN_REGISTER.ReportDispatcher", $"Leave:{employeeCode}:{workDate:yyyyMMdd}", DateTimeOffset.UtcNow);
        var days = Number(leaveRow, "TotalDay");
        return new OfficialHRFacts(employeeCode, workDate, Text(leaveRow, "RequestStatus"),
            null, null, null, null, null, null, null, null, null,
            Text(leaveRow, "LeaveTypeCode"), days, "FVN_REGISTER.ReportDispatcher",
            $"Leave:{Text(leaveRow, "Id") ?? employeeCode + ":" + workDate.ToString("yyyyMMdd")}",
            DateTimeOffset.UtcNow);
    }

    private async Task<ReportResultDto?> GetReportAsync(ReportType type, string employeeCode, DateTime from,
        DateTime to, FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user, CancellationToken ct)
    {
        var result = await _reports.GetReportAsync(new ReportQueryDto
        {
            Type = type, EmployeeCode = employeeCode, FromDate = from, ToDate = to,
            PageNumber = 1, PageSize = 500, IncludeChart = false
        }, user, ct);
        if (!result.IsSuccess || result.Data is null)
            throw new InvalidOperationException("OFFICIAL_HR_REPORT_UNAVAILABLE: " + (result.Message ?? type.ToString()));
        return result.Data;
    }

    private static List<Dictionary<string, object?>> RowsForEmployee(ReportResultDto? report, string employeeCode)
        => report?.Rows.Where(x => string.Equals(Text(x, "EmployeeCode"), employeeCode, StringComparison.OrdinalIgnoreCase)).ToList() ?? new();

    private static decimal? Sum(IEnumerable<Dictionary<string, object?>> rows, string field)
    {
        var values = rows.Select(x => Number(x, field)).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return values.Length == 0 ? null : values.Sum();
    }

    private static string? Text(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return null;
        if (value is JsonElement json) return json.ValueKind == JsonValueKind.String ? json.GetString() : json.ToString();
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static decimal? Number(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return null;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetDecimal(out var parsed)) return parsed;
            value = json.ValueKind == JsonValueKind.String ? json.GetString() : null;
        }
        return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private static bool? Boolean(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return null;
        if (value is bool boolean) return boolean;
        if (value is JsonElement json && (json.ValueKind == JsonValueKind.True || json.ValueKind == JsonValueKind.False))
            return json.GetBoolean();
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var result) ? result : null;
    }

    private static DateTime? DateValue(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is not null
            ? value is DateTime dt ? dt : DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ? parsed : null
            : null;

    private static DateTimeOffset? ToOffset(Dictionary<string, object?> row, string key, DateOnly workDate)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return null;
        if (value is DateTimeOffset dto) return dto;
        if (value is DateTime dt) return new DateTimeOffset(dt);
        if (value is TimeOnly time) return new DateTimeOffset(workDate.ToDateTime(time), TimeSpan.FromHours(7));
        if (value is TimeSpan span) return new DateTimeOffset(workDate.ToDateTime(TimeOnly.MinValue).Add(span), TimeSpan.FromHours(7));
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out var parsedTime))
            return new DateTimeOffset(workDate.ToDateTime(parsedTime), TimeSpan.FromHours(7));
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            return new DateTimeOffset(parsedDate);
        return null;
    }

    private static int ViewFunction(HRCaseType type) => type switch
    {
        HRCaseType.Overtime => SecurityFunctionCodes.OTView,
        HRCaseType.Attendance => SecurityFunctionCodes.AttendanceView,
        HRCaseType.Leave => SecurityFunctionCodes.LeaveView,
        _ => 0
    };

    private static int ReviewFunction(HRCaseType type) => type switch
    {
        HRCaseType.Overtime => SecurityFunctionCodes.OTReconcile,
        HRCaseType.Attendance => SecurityFunctionCodes.AttendanceFeedback,
        HRCaseType.Leave => SecurityFunctionCodes.LeaveApprove,
        _ => 0
    };

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, object body, string actor, CancellationToken ct)
    {
        using var upstream = await SendAiAsync(method, path, JsonContent.Create(body), actor, ct);
        return await RelayAsync(upstream);
    }

    private async Task<HttpResponseMessage> SendAiAsync(HttpMethod method, string path, HttpContent? content, string actor, CancellationToken ct)
    {
        var baseUrl = _configuration["AIPlatform:BaseUrl"];
        var apiKey = _configuration["AIPlatform:ApiKey"];
        var signingKey = _configuration["AIPlatform:ContextSigningKey"];
        var tenant = _configuration["AIPlatform:TenantCode"] ?? "FCCVN";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = JsonContent.Create(new { error = "AIPlatform BaseUrl, ApiKey and ContextSigningKey must be configured." })
            };

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var canonical = string.Join("\n", method.Method.ToUpperInvariant(), path.Split('?')[0], tenant, actor, timestamp, nonce);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path.TrimStart('/'))) { Content = content };
        request.Headers.TryAddWithoutValidation("X-AI-API-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Code", tenant);
        request.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        request.Headers.TryAddWithoutValidation("X-AI-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-AI-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-AI-Signature", signature);
        try { return await _httpClientFactory.CreateClient("FvnAi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (HttpRequestException ex)
        {
            request.Dispose();
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "AI service unavailable.", detail = ex.Message }) };
        }
    }

    private static async Task<IActionResult> RelayAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = payload
        };
    }
}

public sealed record EvidenceUploadRequest(string FileName, string? ContentType, string ContentBase64);
