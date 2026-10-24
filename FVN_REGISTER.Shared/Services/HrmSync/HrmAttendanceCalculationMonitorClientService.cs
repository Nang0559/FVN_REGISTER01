using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.HrmSync;

public sealed class HrmAttendanceCalculationMonitorClientService
{
    private readonly IHttpClientWithAuth _http;
    public HrmAttendanceCalculationMonitorClientService(IHttpClientWithAuth http) => _http = http;

    public Task<ApiResponse<List<HrmAttendanceCalculationMonitorDto>>> GetAsync(CancellationToken ct = default)
        => _http.GetAsync<List<HrmAttendanceCalculationMonitorDto>>("api/hrm-attendance-calculation/monitor", ct, showLoading: false);

    public Task<ApiResponse<HrmAttendanceCalculationMonitorDto>> GetAsync(Guid batchId, CancellationToken ct = default)
        => _http.GetAsync<HrmAttendanceCalculationMonitorDto>($"api/hrm-attendance-calculation/monitor/{batchId}", ct, showLoading: false);

    public Task<ApiResponse<object>> ForceTerminateAsync(Guid batchId, CancellationToken ct = default)
        => _http.PostAsync<object>($"api/hrm-attendance-calculation/monitor/{batchId}/force-terminate", new { }, ct, showLoading: false);
}
