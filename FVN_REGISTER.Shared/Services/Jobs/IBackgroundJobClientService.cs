using FVN_REGISTER.Contract.Dtos.Jobs;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Jobs;

public interface IBackgroundJobClientService
{
    Task<ApiResponse<List<BackgroundJobScheduleDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<BackgroundJobScheduleDto>> UpdateAsync(string jobKey, UpdateBackgroundJobScheduleRequest request, CancellationToken ct = default);
    Task<ApiResponse<object>> RunNowAsync(string jobKey, CancellationToken ct = default);
    Task<ApiResponse<BackgroundJobScheduleDto>> ResetAsync(string jobKey, CancellationToken ct = default);
}
