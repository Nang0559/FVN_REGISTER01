using FVN_REGISTER.Application.Interfaces.Actions;
using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Statics;
using FVN_REGISTER.Application.Interfaces.Dashboards;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Policies;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Contract.Dtos.Equipment;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Utils;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Application.Orchestrators
{
    /// <summary>
    /// Nơi duy nhất lắp ráp Dashboard tổng. Orchestrator không biết chi tiết
    /// nghiệp vụ của từng module; module tự đóng góp qua provider.
    /// </summary>
    public sealed class DashboardOrchestrator : IDashboardOrchestrator
    {
        private readonly IEnumerable<IModuleDashboardProvider> _providers;
        private readonly IApprovalInboxService _approvalInbox;
        private readonly IActionItemService _actions;
        private readonly IAuthorizationService _authorization;
        private readonly ILogger<DashboardOrchestrator> _logger;

        public DashboardOrchestrator(
            IEnumerable<IModuleDashboardProvider> providers,
            IApprovalInboxService approvalInbox,
            IActionItemService actions,
            ILogger<DashboardOrchestrator> logger,
            IAuthorizationService authorization)
        {
            _providers = providers;
            _approvalInbox = approvalInbox;
            _actions = actions;
            _logger = logger;
            _authorization = authorization;
        }

        public async Task<ServiceResult<DashboardResponse>> BuildAsync(
            UserIdentityDto user, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(user.EmployeeCode))
                return ServiceResult<DashboardResponse>.Fail(
                    "Tài khoản chưa liên kết với hồ sơ nhân viên.");

            try
            {
                var response = new DashboardResponse
                {
                    // Manager Workspace is capability-driven. ManagedScope limits the data,
                    // but never grants the capability itself.
                    ShowManagerView =
                        await _authorization.HasManagementAsync(user, SecurityFunctionCodes.LeaveView, ct)
                        || await _authorization.HasManagementAsync(user, SecurityFunctionCodes.OTView, ct)
                        || await _authorization.HasManagementAsync(user, SecurityFunctionCodes.TripView, ct)
                        || await _authorization.HasManagementAsync(user, SecurityFunctionCodes.EquipmentView, ct)
                        || await _authorization.HasManagementAsync(user, SecurityFunctionCodes.AttendanceView, ct)
                };

                // Dashboard access may come from DashboardView OR a module approver capability.
                var canOpenDashboard =
                    await _authorization.HasAsync(user, SecurityFunctionCodes.DashboardView, ct)
                    || await _authorization.HasAsync(user, SecurityFunctionCodes.LeaveApprove, ct)
                    || await _authorization.HasAsync(user, SecurityFunctionCodes.OTApprove, ct)
                    || await _authorization.HasAsync(user, SecurityFunctionCodes.TripApprove, ct);

                if (!canOpenDashboard)
                    return ServiceResult<DashboardResponse>.Fail("Tài khoản chưa được cấp quyền xem Dashboard.");

                var rawWidgets = new List<WidgetCounterDto>();

                // Providers share the request-scoped UnitOfWork/DbContext.
                // EF Core DbContext is not thread-safe, so providers must execute
                // sequentially within this scope.
                foreach (var provider in _providers)
                {
                    if (!await CanBuildProviderAsync(user, provider, ct))
                        continue;

                    var contribution = await provider.GetContributionAsync(user, ct);
                    rawWidgets.AddRange(contribution.Widgets);

                    switch (contribution.Module)
                    {
                        case RequestModule.Leave when contribution.Detail is LeaveDashboardDto leave:
                            response.Leave = leave;
                            response.DeptWarning = leave.DeptWarning;
                            response.DepartmentStatistics = leave.DepartmentStatistics;
                            break;

                        case RequestModule.Overtime when contribution.Detail is OTDashboardDto overtime:
                            response.OT = overtime;
                            break;

                        case RequestModule.Trip:
                            response.Trip = contribution.Detail;
                            break;

                        case RequestModule.Equipment when contribution.Detail is List<EquipmentRequestDto> equipment:
                            response.Equipment = equipment;
                            break;

                        default:
                            _logger.LogWarning(
                                "[DASHBOARD] Provider returned unsupported module/detail type: {Module} / {DetailType}",
                                contribution.Module,
                                contribution.Detail?.GetType().Name ?? "null");
                            break;
                    }
                }

                // Approval-pending summary cards are only meaningful for users who can
                // approve that module. Do this on the server so ordinary users never
                // receive these counters in the Dashboard response.
                var canApproveLeave = await _authorization.HasAsync(
                    user, SecurityFunctionCodes.LeaveApprove, ct);
                var canApproveOT = await _authorization.HasAsync(
                    user, SecurityFunctionCodes.OTApprove, ct);
                var canApproveTrip = await _authorization.HasAsync(
                    user, SecurityFunctionCodes.TripApprove, ct);

                rawWidgets = rawWidgets
                    .Where(widget => !IsApprovalPendingWidget(widget, canApproveLeave, canApproveOT, canApproveTrip))
                    .ToList();

                response.Widgets = DashboardWidgetPolicy.Arrange(user, rawWidgets);

                // Shared Action inbox is part of the authenticated user's dashboard.
                // Action status does not replace any business workflow status.
                var actionCount = await _actions.GetCountAsync(user.EmployeeCode, user.UserId, ct);
                var actions = await _actions.GetMineAsync(user.EmployeeCode, user.UserId, includeCompleted: false, ct);
                response.ActionCount = actionCount;
                response.Actions = actions.Take(5).ToList();

                // Approval inbox is independent from Manager Workspace.
                // A user may be an approver without ManagedScope; the inbox service
                // resolves actual approval policy/route and returns only actionable items.
                var pendingResult = await _approvalInbox.GetPendingAsync(user, ct);
                response.PendingApprovals = pendingResult.IsSuccess
                    ? pendingResult.Data ?? new List<PendingApprovalGroupDto>()
                    : new List<PendingApprovalGroupDto>();

                return ServiceResult<DashboardResponse>.Ok(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DASHBOARD] Aggregation failed for UserId={UserId}", user.UserId);
                return ServiceResult<DashboardResponse>.Fail(
                    "Không thể tải Dashboard.");
            }
        }

        private static bool IsApprovalPendingWidget(
            WidgetCounterDto widget,
            bool canApproveLeave,
            bool canApproveOT,
            bool canApproveTrip)
        {
            var title = (widget.Title ?? string.Empty).Trim();

            // Match the existing dashboard widget titles (Vietnamese and known
            // English/resource-key variants). Personal balances/history widgets
            // are intentionally not affected.
            if (title.Equals("Đơn nghỉ chờ duyệt", StringComparison.OrdinalIgnoreCase)
                || title.Equals("Leave pending approval", StringComparison.OrdinalIgnoreCase)
                || title.Equals("dashboard.leavePending", StringComparison.OrdinalIgnoreCase))
                return !canApproveLeave;

            if (title.Equals("Đơn OT chờ duyệt", StringComparison.OrdinalIgnoreCase)
                || title.Equals("OT pending approval", StringComparison.OrdinalIgnoreCase)
                || title.Equals("dashboard.otPending", StringComparison.OrdinalIgnoreCase))
                return !canApproveOT;

            if (title.Equals("Công tác đang chờ", StringComparison.OrdinalIgnoreCase)
                || title.Equals("Công tác chờ duyệt", StringComparison.OrdinalIgnoreCase)
                || title.Equals("Trip pending approval", StringComparison.OrdinalIgnoreCase)
                || title.Equals("dashboard.tripPending", StringComparison.OrdinalIgnoreCase))
                return !canApproveTrip;

            return false;
        }

        private async Task<bool> CanBuildProviderAsync(
            UserIdentityDto user, IModuleDashboardProvider provider, CancellationToken ct)
        {
            // Approvers can see their module's scoped dashboard contribution without
            // an unrelated DashboardView grant. Providers still enforce data scope.
            if (await _authorization.HasAsync(user, SecurityFunctionCodes.DashboardView, ct)
                && await _authorization.HasAsync(user, provider.RequiredFunctionCode, ct))
                return true;

            var approvalFunction = provider.Module switch
            {
                RequestModule.Leave => SecurityFunctionCodes.LeaveApprove,
                RequestModule.Overtime => SecurityFunctionCodes.OTApprove,
                RequestModule.Trip => SecurityFunctionCodes.TripApprove,
                _ => 0
            };

            return approvalFunction != 0
                && await _authorization.HasAsync(user, approvalFunction, ct);
        }
    }
}
