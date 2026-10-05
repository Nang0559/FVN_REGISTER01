using FVN_REGISTER.Contract.Dtos.SystemUpdate;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.SystemUpdate
{
    public interface ISystemUpdateService
    {
        Task<ServiceResult<SystemUpdateOverviewDto>> GetOverviewAsync(CancellationToken ct = default);
        Task<ServiceResult<PatchUploadResultDto>> SaveUploadAsync(
            Stream package, string originalFileName, int userId, string userName, CancellationToken ct = default);
    }
}