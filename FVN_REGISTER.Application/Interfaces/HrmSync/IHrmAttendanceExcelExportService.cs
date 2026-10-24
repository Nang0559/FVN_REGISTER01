using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.HrmSync;

public interface IHrmAttendanceExcelExportService
{
    Task<ServiceResult<byte[]>> ExportAttendanceAsync(Guid batchId, CancellationToken ct = default);
    Task<ServiceResult<byte[]>> ExportAttendanceByJobAsync(Guid jobId, CancellationToken ct = default);
    Task<ServiceResult<byte[]>> ExportOtAsync(Guid batchId, CancellationToken ct = default);
    Task<ServiceResult<byte[]>> ExportOtByJobAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Xuất bảng chấm công theo bộ phận (null = tất cả) + khoảng ngày, trực tiếp từ dữ liệu đã tính; không cần job/batch.</summary>
    Task<ServiceResult<byte[]>> ExportAttendanceByRangeAsync(int? deptCode, DateTime fromDate, DateTime toDate, CancellationToken ct = default);

    /// <summary>Xuất bảng làm thêm giờ theo bộ phận (null = tất cả) + khoảng ngày, trực tiếp từ dữ liệu đã tính; không cần job/batch.</summary>
    Task<ServiceResult<byte[]>> ExportOtByRangeAsync(int? deptCode, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}