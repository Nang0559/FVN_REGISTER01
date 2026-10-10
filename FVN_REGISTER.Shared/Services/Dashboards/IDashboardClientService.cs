using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Dtos.Dashboard;

namespace FVN_REGISTER.Shared.Services.Dashboards
{
    public interface IDashboardClientService
    {
        Task<ApiResponse<DashboardResponse>> GetDashboardDataAsync(CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<AttendanceShiftDashboardDto>>> GetAttendanceShiftDashboardAsync(DateOnly workDate, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<DepartmentDashboardDetailDto>>> GetDepartmentDetailsAsync(string kind, DateOnly workDate, CancellationToken ct = default);
    }
}
