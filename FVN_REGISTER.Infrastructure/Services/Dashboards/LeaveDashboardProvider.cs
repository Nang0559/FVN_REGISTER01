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
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

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
        private readonly IUnitOfWork _uow;

        public RequestModule Module => RequestModule.Leave;
        public int RequiredFunctionCode => SecurityFunctionCodes.DashboardView;

        public LeaveDashboardProvider(
            ILeaveQueryService leaveQuery,
            IStatisticsService statistics,
            IAuthorizationService authorization,
            IUnitOfWork uow)
        {
            _leaveQuery = leaveQuery;
            _statistics = statistics;
            _authorization = authorization;
            _uow = uow;
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

            // F03ApprovalPolicies is the source of truth for approval department scope.
            // Match active policy + approver position + exact approval level + request type.
            // DeptCode is the requester department governed by the policy, not the approver's own department.
            var activePolicies = await _uow.Repository<F03ApprovalPolicy>().Query()
                .AsNoTracking()
                .Where(p => p.IsActive == true
                    && p.ApprovalPositionCode == user.PositionCode
                    && p.Level == user.LevelApprove
                    && (p.RequestType == RequestModule.Leave
                        || p.RequestType == RequestModule.Overtime
                        || p.RequestType == RequestModule.Trip))
                .Select(p => new { p.RequestType, p.DeptCode })
                .Distinct()
                .ToListAsync(ct);

            var leaveDepartments = activePolicies.Where(p => p.RequestType == RequestModule.Leave)
                .Select(p => p.DeptCode).Distinct().ToHashSet();
            var otDepartments = activePolicies.Where(p => p.RequestType == RequestModule.Overtime)
                .Select(p => p.DeptCode).Distinct().ToHashSet();
            var tripDepartments = activePolicies.Where(p => p.RequestType == RequestModule.Trip)
                .Select(p => p.DeptCode).Distinct().ToHashSet();
            var approvalDepartments = leaveDepartments
                .Concat(otDepartments).Concat(tripDepartments).ToHashSet();

            if (approvalDepartments.Count > 0)
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var allStatistics = await _statistics.GetLeaveStatisticsAsync(
                    includeCompanyTotal: false, ct);

                var todayLeaveByDept = leaveDepartments.Count == 0
                    ? new Dictionary<int, int>()
                    : await _uow.Repository<F03LeaveDay>().Query()
                        .AsNoTracking()
                        .Where(l => l.IsActive == true
                            && l.RequestStatus == ApprovalStatus.Approved
                            && l.StartTime < tomorrow && l.EndTime >= today)
                        .Join(_uow.Repository<F03Employee>().Query().Where(e => e.IsActive == true),
                            l => l.EmployeeCode, e => e.EmployeeCode,
                            (l, e) => new { l.EmployeeCode, e.DeptCode })
                        .Where(x => leaveDepartments.Contains(x.DeptCode))
                        .GroupBy(x => x.DeptCode)
                        .Select(g => new { DeptCode = g.Key, Count = g.Select(x => x.EmployeeCode).Distinct().Count() })
                        .ToDictionaryAsync(x => x.DeptCode, x => x.Count, ct);

                var todayOtByDept = otDepartments.Count == 0
                    ? new Dictionary<int, int>()
                    : await _uow.Repository<F03OTEmployee>().Query()
                        .AsNoTracking()
                        .Where(e => e.IsActive == true
                            && e.OTRequest.IsActive == true
                            && e.OTRequest.RequestStatus == ApprovalStatus.Approved
                            && e.OTRequest.OTDate >= today && e.OTRequest.OTDate < tomorrow
                            && e.DeptCode.HasValue)
                        .Where(e => otDepartments.Contains(e.DeptCode!.Value))
                        .GroupBy(e => e.DeptCode!.Value)
                        .Select(g => new { DeptCode = g.Key, Count = g.Select(x => x.EmployeeCode).Distinct().Count() })
                        .ToDictionaryAsync(x => x.DeptCode, x => x.Count, ct);

                var todayTripByDept = tripDepartments.Count == 0
                    ? new Dictionary<int, int>()
                    : await _uow.Repository<F03TripRequest>().Query()
                        .AsNoTracking()
                        .Where(t => t.IsActive == true
                            && t.RequestStatus == ApprovalStatus.Approved
                            && t.StartDate < tomorrow && t.EndDate >= today)
                        .Join(_uow.Repository<F03Employee>().Query().Where(e => e.IsActive == true),
                            t => t.EmployeeCode, e => e.EmployeeCode,
                            (t, e) => new { t.EmployeeCode, e.DeptCode })
                        .Where(x => tripDepartments.Contains(x.DeptCode))
                        .GroupBy(x => x.DeptCode)
                        .Select(g => new { DeptCode = g.Key, Count = g.Select(x => x.EmployeeCode).Distinct().Count() })
                        .ToDictionaryAsync(x => x.DeptCode, x => x.Count, ct);

                foreach (var row in allStatistics)
                {
                    if (!int.TryParse(row.DepartmentId, out var departmentCode)
                        || !approvalDepartments.Contains(departmentCode))
                        continue;

                    if (leaveDepartments.Contains(departmentCode))
                        todayLeaveByDept.TryGetValue(departmentCode, out row.TodayLeaveEmployeesCount);
                    if (otDepartments.Contains(departmentCode))
                        todayOtByDept.TryGetValue(departmentCode, out row.TodayOTEmployeesCount);
                    if (tripDepartments.Contains(departmentCode))
                        todayTripByDept.TryGetValue(departmentCode, out row.TodayTripEmployeesCount);

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
