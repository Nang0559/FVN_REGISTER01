using FVN_REGISTER.Contract.Requests.LateEarly;
using FVN_REGISTER.Contract.Dtos.OT;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Responses.LateEarly;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.LateEarly;

public interface ILateEarlyClientService
{
    Task<ApiResponse<List<OTEmployeeDto>>> SearchEmployeesAsync(string? keyword, int? deptCode = null, CancellationToken ct = default);
    Task<ApiResponse<LateEarlyRequestDto>> CreateAsync(CreateLateEarlyRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<LateEarlyRequestDto>> SubmitAsync(int id, List<FVN_REGISTER.Contract.Requests.Approvals.ApprovalSelectionDto>? selections = null, CancellationToken ct = default);
    Task<ApiResponse<List<LateEarlyRequestDto>>> GetMineAsync(CancellationToken ct = default);
    Task<ApiResponse<object>> CancelAsync(int id, string? reason, CancellationToken ct = default);
}

public sealed class LateEarlyClientService : ILateEarlyClientService
{
    private readonly IHttpClientWithAuth _http;
    public LateEarlyClientService(IHttpClientWithAuth http) => _http = http;

    public Task<ApiResponse<List<OTEmployeeDto>>> SearchEmployeesAsync(string? keyword, int? deptCode = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword))
            query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (deptCode.HasValue)
            query.Add($"deptCode={deptCode.Value}");
        var url = query.Count == 0
            ? "api/late-early/search-employees"
            : $"api/late-early/search-employees?{string.Join("&", query)}";
        return _http.GetAsync<List<OTEmployeeDto>>(url, ct, showLoading: false);
    }

    public Task<ApiResponse<LateEarlyRequestDto>> CreateAsync(CreateLateEarlyRequestDto request, CancellationToken ct = default)
        => _http.PostAsync<LateEarlyRequestDto>("api/late-early", request, ct);

    public Task<ApiResponse<LateEarlyRequestDto>> SubmitAsync(int id, List<FVN_REGISTER.Contract.Requests.Approvals.ApprovalSelectionDto>? selections = null, CancellationToken ct = default)
        => _http.PostAsync<LateEarlyRequestDto>($"api/late-early/{id}/submit", selections ?? new(), ct);

    public Task<ApiResponse<List<LateEarlyRequestDto>>> GetMineAsync(CancellationToken ct = default)
        => _http.GetAsync<List<LateEarlyRequestDto>>("api/late-early/mine", ct);

    public Task<ApiResponse<object>> CancelAsync(int id, string? reason, CancellationToken ct = default)
        => _http.PostAsync<object>($"api/late-early/{id}/cancel", new { Reason = reason }, ct);
}
