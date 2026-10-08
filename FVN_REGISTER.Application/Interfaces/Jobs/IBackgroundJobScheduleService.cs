using FVN_REGISTER.Contract.Dtos.Jobs;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Jobs;

/// <summary>Admin API behind the "Background job schedule" screen.</summary>
public interface IBackgroundJobScheduleService
{
    Task<ServiceResult<List<BackgroundJobScheduleDto>>> GetAllAsync(CancellationToken ct = default);

    Task<ServiceResult<BackgroundJobScheduleDto>> UpdateAsync(
        string jobKey, UpdateBackgroundJobScheduleRequest request, int? modifiedBy, CancellationToken ct = default);

    /// <summary>Flags the job to run at the worker's next poll (within ~15 seconds).</summary>
    Task<ServiceResult> RequestRunNowAsync(string jobKey, int? modifiedBy, CancellationToken ct = default);

    Task<ServiceResult<BackgroundJobScheduleDto>> ResetToDefaultAsync(
        string jobKey, int? modifiedBy, CancellationToken ct = default);
}
