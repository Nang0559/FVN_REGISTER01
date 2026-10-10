using FVN_REGISTER.Application.Interfaces.Leaves;
using FVN_REGISTER.Application.Interfaces.Statics;
using FVN_REGISTER.Application.Interfaces.Dashboards;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Contract.Dtos.Leaves;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Infrastructure.Services.Dashboards
{
    /// <summary>
    /// Leave-specific dashboard contribution. All Leave data access stays behind
    /// existing query/statistics ports; the provider does not introduce new EF queries.
    /// </summary>
    public sealed class LeaveDashboardProvider : IModuleDashboardProvider
    {
        private readonly ILeaveQueryService _leaveQuery;
        private readonly IStatisticsService _statistics;
        private readonly IAuthorizationService _authorization;

        public RequestModule Module => RequestModule.Leave;
        public int RequiredFunctionCode => SecurityFunctionCodes.DashboardView;

        public LeaveDashboardProvider(
            ILeaveQueryService leaveQuery,
            IStatisticsService statistics,
            IAuthorizationService authorization)
        {
            _leaveQuery = leaveQuery;
            _statistics = statistics;
            _authorization = authorization;
        }

        public async Task<ModuleDashboardContribution> GetContributionAsync(
            UserIdentityDto user, CancellationToken ct = default)
        {
            var year = DateTime.Now.Year;
            var canPersonal = await _authorization.HasPersonalAsync(user, SecurityFunctionCodes.LeaveView, ct);

            var widgets = canPersonal
                ? new List<WidgetCounterDto>((await _leaveQuery.GetMyWidgetsAsync(user.EmployeeCode!, ct)).Data ?? new List<WidgetCounterDto>())
                : new List<WidgetCounterDto>();

            var personalBalance = canPersonal
                ? (await _leaveQuery.GetSimpleBalanceAsync(user.EmployeeCode!, year, ct)).Data
                : null;
            var recentSummary = canPersonal
                ? (await _leaveQuery.GetRecentSummaryAsync(user.EmployeeCode!, 5, ct)).Data ?? new List<LeaveSummaryDto>()
                : new List<LeaveSummaryDto>();

            AbsenceWarningDto? deptWarning = null;
            var departmentStatistics = new List<LeaveStatisticsDto>();

            var canManageDepartment = user.DeptCode != null
                && await _authorization.CanAccessAsync(
                    user, SecurityFunctionCodes.LeaveView, null, user.DeptCode, ct);

            // Department rows are visible only within the authenticated user's own
            // management scope or explicitly configured Leave approval scope.
            if (canManageDepartment)
            {
                deptWarning = await _statistics.GetAbsenceWarningAsync(
                    user.DeptCode!.Value, ct);

                var scope = await _authorization.GetScopeAsync(
                    user.UserId, SecurityFunctionCodes.LeaveView, ct);

                if (string.Equals(scope, AuthorizationScopeCodes.All, StringComparison.OrdinalIgnoreCase))
                {
                    departmentStatistics = await _statistics.GetLeaveStatisticsAsync(
                        includeCompanyTotal: true, ct);
                }
                else
                {
                    departmentStatistics.Add(
                        await _statistics.GetDepartmentStatisticsAsync(
                            user.DeptCode!.Value, ct));
                }
            }

            // These are Leave/attendance aggregates: OT/Trip approval scope must not
            // accidentally grant access to Leave data.
            if (await _authorization.HasAsync(user, SecurityFunctionCodes.LeaveApprove, ct))
            {
                var approvalScope = await _authorization.GetScopeAsync(
                    user.UserId, SecurityFunctionCodes.LeaveApprove, ct);
                var allStatistics = await _statistics.GetLeaveStatisticsAsync(
                    includeCompanyTotal: true, ct);
                var canSeeAllDepartments = string.Equals(
                    approvalScope, AuthorizationScopeCodes.All, StringComparison.OrdinalIgnoreCase);

                foreach (var row in allStatistics)
                {
                    if (!int.TryParse(row.DepartmentId, out var departmentCode))
                        continue;

                    var allowed = canSeeAllDepartments
                        || await _authorization.CanAccessAsync(
                            user, SecurityFunctionCodes.LeaveApprove, null, departmentCode, ct);
                    if (!allowed)
                        continue;

                    row.EmployeeLeaves = new();
                    row.Departments = new();
                    departmentStatistics.Add(row);
                }
            }

            departmentStatistics = departmentStatistics
                .GroupBy(x => x.DepartmentId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            // Summary access never needs employee-level records or department selectors.
            foreach (var row in departmentStatistics)
            {
                row.EmployeeLeaves = new();
                row.Departments = new();
            }

            var detail = new LeaveDashboardDto
            {
                Widgets = widgets,
                PersonalBalance = personalBalance,
                RecentRequests = recentSummary,
                DeptWarning = deptWarning,
                DepartmentStatistics = departmentStatistics
            };

            return new ModuleDashboardContribution
            {
                Module = Module,
                Widgets = widgets,
                Detail = detail
            };
        }
    }
}
