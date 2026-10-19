using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.HrmSync;

public sealed class HrmAttendanceCalculationJobClientService
{
    private readonly IHttpClientWithAuth _http;
    public HrmAttendanceCalculationJobClientService(IHttpClientWithAuth http) => _http = http;

    public Task<ApiResponse<List<HrmAttendanceCalculationJobDto>>> GetAsync(CancellationToken ct = default)
        => _http.GetAsync<List<HrmAttendanceCalculationJobDto>>("api/hrm-attendance-calculation/jobs", ct, showLoading: false);

    public Task<ApiResponse<HrmAttendanceCalculationJobDto>> GetAsync(Guid jobId, CancellationToken ct = default)
        => _http.GetAsync<HrmAttendanceCalculationJobDto>($"api/hrm-attendance-calculation/jobs/{jobId}", ct, showLoading: false);

    public Task<ApiResponse<HrmAttendanceCalculationJobDto>> StartAsync(HrmAttendanceCalculationJobCommandDto request, CancellationToken ct = default)
        => _http.PostAsync<HrmAttendanceCalculationJobDto>("api/hrm-attendance-calculation/jobs", request, ct, showLoading: false);

    public Task<ApiResponse<object>> CancelAsync(Guid jobId, CancellationToken ct = default)
        => _http.PostAsync<object>($"api/hrm-attendance-calculation/jobs/{jobId}/cancel", new { }, ct, showLoading: false);

    public Task<ApiResponse<object>> ResumeAsync(Guid jobId, CancellationToken ct = default)
        => _http.PostAsync<object>($"api/hrm-attendance-calculation/jobs/{jobId}/resume", new { }, ct, showLoading: false);

    public Task<ApiResponse<object>> ForceTerminateAsync(Guid jobId, CancellationToken ct = default)
        => _http.PostAsync<object>($"api/hrm-attendance-calculation/jobs/{jobId}/force-terminate", new { }, ct, showLoading: false);
}
