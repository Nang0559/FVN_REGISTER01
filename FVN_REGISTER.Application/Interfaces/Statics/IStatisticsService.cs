using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.Leaves;
using FVN_REGISTER.Contract.Dtos.MasterData;
using FVN_REGISTER.Core.Utils;

namespace FVN_REGISTER.Application.Interfaces.Statics
{
    public interface IStatisticsService
    {
        Task<LeaveStatisticsDto> GetDepartmentStatisticsAsync(int deptCode, CancellationToken ct = default);
        Task<List<LeaveStatisticsDto>> GetStatisticsByTimeRangeAsync(int[]? departments, TimeRange timeRange, CancellationToken ct = default);
        Task<AbsenceWarningDto> GetAbsenceWarningAsync(int deptCode, CancellationToken ct = default);
        Task<List<WidgetCounterDto>> GetCompanyDashboardWidgetsAsync(CancellationToken ct = default);
        Task<List<WidgetCounterDto>> GetDeptDashboardWidgetsAsync(int deptCode, CancellationToken ct = default);
        Task<List<LeaveStatisticsDto>> GetLeaveStatisticsAsync(
            bool includeCompanyTotal = false,
            CancellationToken ct = default);
    }
}