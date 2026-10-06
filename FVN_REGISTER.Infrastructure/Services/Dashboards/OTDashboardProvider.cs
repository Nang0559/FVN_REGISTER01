using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Statics;
using FVN_REGISTER.Application.Interfaces.Dashboards;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Contract.Dtos.OT;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;


namespace FVN_REGISTER.Infrastructure.Services.Dashboards
{ 
    public class OTDashboardProvider : IModuleDashboardProvider
    {
        private readonly IOTQueryService _otQuery;
        private readonly IAuthorizationService _authorization;

        public RequestModule Module => RequestModule.Overtime;
        public int RequiredFunctionCode => SecurityFunctionCodes.OTView;

        public OTDashboardProvider(IOTQueryService otQuery, IAuthorizationService authorization)
        {
            _otQuery = otQuery;
            _authorization = authorization;
        }

        public async Task<ModuleDashboardContribution> GetContributionAsync(
     UserIdentityDto user, CancellationToken ct = default)
        {
            var year = DateTime.Now.Year;
            var month = DateTime.Now.Month;
            var canPersonal = await _authorization.HasPersonalAsync(user, SecurityFunctionCodes.OTView, ct);

            var widgets = canPersonal
                ? new List<WidgetCounterDto>((await _otQuery.GetMyWidgetsAsync(user.EmployeeCode!, ct)).Data ?? new List<WidgetCounterDto>())
                : new List<WidgetCounterDto>();

            var personalBalance = canPersonal
                ? (await _otQuery.GetSimpleBalanceAsync(user.EmployeeCode!, year, ct)).Data
                : null;
            var recentSummary = canPersonal
                ? (await _otQuery.GetRecentSummaryAsync(user.EmployeeCode!, 5, ct)).Data ?? new List<OTSummaryDto>()
                : new List<OTSummaryDto>();

            List<OTBalanceDto> nearLimitEmployees = new();
            if (user.DeptCode != null
                && await _authorization.CanAccessAsync(user, SecurityFunctionCodes.OTView, null, user.DeptCode, ct))
            {
                nearLimitEmployees = await _otQuery.GetDeptNearLimitAsync(user.DeptCode, year, month, ct) ?? new List<OTBalanceDto>();

                // MỚI: thêm widget đếm số để hiện ngay trên dashboard tổng quan,
                // đối xứng với cách LeaveDashboardProvider thêm dept-widgets vào Widgets list.
                // Không tự tạo cảnh báo nếu không có ai gần vượt — tránh nhiễu Dashboard.
                if (nearLimitEmployees.Count > 0)
                {
                    widgets.Add(new WidgetCounterDto
                    {
                        Title = "NV gần vượt giới hạn OT",
                        Value = nearLimitEmployees.Count.ToString(),
                        Icon = "Warning",
                        Color = "Warning",
                        Link = "/ot/dept-status",
                        IsPersonal = false   // widget quản lý, không phải "của tôi"
                    });
                }
            }

            var detail = new OTDashboardDto
            {
                Widgets = widgets,
                PersonalBalance = personalBalance,
                RecentRequests = recentSummary,
                NearLimitEmployees = nearLimitEmployees
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
