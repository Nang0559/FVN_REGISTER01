using FVN_REGISTER.Application.Interfaces.Common;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.OT;
using FVN_REGISTER.Contract.Requests.OT;

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

        Task<int?> GetEmployeeDeptCodeAsync(string employeeCode, CancellationToken ct = default);

        Task<List<OTBalanceDto>> GetDeptNearLimitAsync(
            int deptCode, int year, int month, CancellationToken ct = default);
    }
}