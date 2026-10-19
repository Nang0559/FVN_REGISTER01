using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
namespace FVN_REGISTER.Shared.Services.HrmSync;
public interface IHrmAttendanceCalculationClientService
{
 Task<ApiResponse<HrmAttendanceCalculationJobDto>> CalculateAsync(HrmAttendanceCalculationRequestDto request,CancellationToken ct=default);
 Task<ApiResponse<HrmAttendanceCalculationJobDto>> ForceRecalculateSelfAsync(HrmAttendanceCalculationRequestDto request,CancellationToken ct=default);
 Task<ApiResponse<byte[]>> ExportAttendanceAsync(Guid batchId,CancellationToken ct=default);
 Task<ApiResponse<byte[]>> ExportOtAsync(Guid batchId,CancellationToken ct=default);
 Task<ApiResponse<byte[]>> ExportAttendanceByJobAsync(Guid jobId,CancellationToken ct=default);
 Task<ApiResponse<byte[]>> ExportOtByJobAsync(Guid jobId,CancellationToken ct=default);
 Task<ApiResponse<byte[]>> ExportAttendanceByRangeAsync(int? deptCode,DateTime fromDate,DateTime toDate,CancellationToken ct=default);
 Task<ApiResponse<byte[]>> ExportOtByRangeAsync(int? deptCode,DateTime fromDate,DateTime toDate,CancellationToken ct=default);
}
