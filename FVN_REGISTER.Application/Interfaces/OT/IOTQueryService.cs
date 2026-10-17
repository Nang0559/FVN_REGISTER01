using FVN_REGISTER.Application.Interfaces.Common;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.OT;
using FVN_REGISTER.Contract.Requests.OT;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Interfaces.OT
{
    public interface IOTQueryService
        : IRequestQueryService<OTSummaryDto, OTBalanceDto, OTRequestDto>
    {
        Task<OTCombinedDataDto> GetCombinedDataAsync(
            string employeeCode, int deptCode, int year, int month, CancellationToken ct = default);
        Task<OTValidationResultDto> ValidateHoursAsync(OTRequestUpsertDto model, CancellationToken ct = default, bool checkDuplicateRegistration = true);

        /// <param name="checkDuplicateRegistration">true (mặc định): báo lỗi nếu nhân viên đã có đơn OT trong ngày (mỗi nhân viên 1 đơn/ngày).</param>
        Task<OTLimitPreviewDto> GetLimitPreviewAsync(OTRequestUpsertDto model, CancellationToken ct = default, bool checkDuplicateRegistration = true);

        Task<OTBalanceDto> GetBalanceAsync(string employeeCode, int year, int month, CancellationToken ct = default);

        Task<List<OTEmployeeDto>> GetDeptEmployeesAsync(int deptCode, CancellationToken ct = default);

        /// <summary>
        /// Tìm nhân viên để thêm vào đơn OT thay cho việc nạp cả phòng ban rồi xoá bớt.
        /// <paramref name="codes"/> có giá trị: khớp chính xác theo mã (dán danh sách); ngược lại tìm theo <paramref name="keyword"/> (mã hoặc tên).
        /// </summary>
        Task<List<OTEmployeeDto>> SearchEmployeesAsync(int deptCode, string? keyword, IReadOnlyList<string>? codes, int take = 20, CancellationToken ct = default);

        /// <summary>
        /// Lịch sử OT của chính <paramref name="employeeCode"/> (đơn mình tạo + đơn người khác đăng ký cho mình),
        /// không phụ thuộc phạm vi xem phòng ban.
        /// </summary>
        Task<ServiceResult<PaginationResult<OTSummaryDto>>> GetMyHistoryAsync(
            string employeeCode, ApprovalStatus? status, DateTime? fromDate, DateTime? toDate,
            int page, int pageSize, CancellationToken ct = default);

        Task<int?> GetEmployeeDeptCodeAsync(string employeeCode, CancellationToken ct = default);

        Task<List<OTBalanceDto>> GetDeptNearLimitAsync(
            int deptCode, int year, int month, CancellationToken ct = default);
    }
}