using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;
namespace FVN_REGISTER.Shared.Services.HrmSync;
public sealed class HrmAttendanceCalculationClientService:IHrmAttendanceCalculationClientService
{
 private readonly IHttpClientWithAuth _http;
 public HrmAttendanceCalculationClientService(IHttpClientWithAuth http)=>_http=http;
 public Task<ApiResponse<HrmAttendanceCalculationJobDto>> CalculateAsync(HrmAttendanceCalculationRequestDto request,CancellationToken ct=default)
  => _http.PostAsync<HrmAttendanceCalculationJobDto>("api/hrm-attendance-calculation/calculate",request,ct);
 public Task<ApiResponse<HrmAttendanceCalculationJobDto>> ForceRecalculateSelfAsync(HrmAttendanceCalculationRequestDto request,CancellationToken ct=default)
  => _http.PostAsync<HrmAttendanceCalculationJobDto>("api/hrm-attendance-calculation/force-recalculate-self",request,ct);
 public Task<ApiResponse<byte[]>> ExportAttendanceAsync(Guid batchId,CancellationToken ct=default)
  => _http.GetFileAsync($"api/hrm-attendance-calculation/export/attendance/{batchId}",ct);
 public Task<ApiResponse<byte[]>> ExportOtAsync(Guid batchId,CancellationToken ct=default)
  => _http.GetFileAsync($"api/hrm-attendance-calculation/export/ot/{batchId}",ct);
 public Task<ApiResponse<byte[]>> ExportAttendanceByJobAsync(Guid jobId,CancellationToken ct=default)
  => _http.GetFileAsync($"api/hrm-attendance-calculation/export/attendance/job/{jobId}",ct);
 public Task<ApiResponse<byte[]>> ExportOtByJobAsync(Guid jobId,CancellationToken ct=default)
  => _http.GetFileAsync($"api/hrm-attendance-calculation/export/ot/job/{jobId}",ct);
 public Task<ApiResponse<byte[]>> ExportAttendanceByRangeAsync(int? deptCode,DateTime fromDate,DateTime toDate,CancellationToken ct=default)
  => _http.GetFileAsync(BuildRangeUrl("attendance",deptCode,fromDate,toDate),ct);
 public Task<ApiResponse<byte[]>> ExportOtByRangeAsync(int? deptCode,DateTime fromDate,DateTime toDate,CancellationToken ct=default)
  => _http.GetFileAsync(BuildRangeUrl("ot",deptCode,fromDate,toDate),ct);
 private static string BuildRangeUrl(string kind,int? deptCode,DateTime fromDate,DateTime toDate)
  => $"api/hrm-attendance-calculation/export/{kind}?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}"+(deptCode.HasValue?$"&deptCode={deptCode.Value}":string.Empty);
}
