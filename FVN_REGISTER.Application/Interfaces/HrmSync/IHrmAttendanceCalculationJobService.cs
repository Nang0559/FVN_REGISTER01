using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.HrmSync;

public interface IHrmAttendanceCalculationJobService
{
    Task<ServiceResult<HrmAttendanceCalculationJobDto>> EnqueueAsync(HrmAttendanceCalculationJobCommandDto request,string triggeredBy,CancellationToken ct=default,bool skipCoveredDates=false,byte priority=1);
    Task<ServiceResult<HrmAttendanceCalculationJobDto>> GetAsync(Guid jobId,CancellationToken ct=default);
    Task<ServiceResult<List<HrmAttendanceCalculationJobDto>>> ListAsync(CancellationToken ct=default);
    Task<ServiceResult> RequestCancelAsync(Guid jobId,string requestedBy,CancellationToken ct=default);
    Task<ServiceResult> ResumeAsync(Guid jobId,string requestedBy,CancellationToken ct=default);
}
