using FVN_REGISTER.Contract.Dtos.MasterData;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;


namespace FVN_REGISTER.Shared.Utils.Extentions
{
    public static class RequestModuleUIExtensions
    {
        public static string GetEventColor(this RequestModule module) => module switch
        {
            RequestModule.Leave => "#FF9800",
            RequestModule.Overtime => "#2196F3",
            RequestModule.Trip => "#3F51B5",
            RequestModule.Attendance => "#009688",
            RequestModule.Equipment => "#795548",
            RequestModule.Payroll => "#4CAF50",
            RequestModule.AccessChange => "#9C27B0",
            RequestModule.Endpoint => "#607D8B",
            _ => "#9E9E9E"
        };

        public static string GetEventColor(this CalendarEventDto e) => e.Module.GetEventColor();

        public static UIStyle GetStatusStyle(this RequestModule module) => module switch
        {
            RequestModule.Leave => new UIStyle
            {
                Icon = IconConstants.Module.Leave,
                Color = "info",
                CssClass = "badge-leave"
            },
            RequestModule.Overtime => new UIStyle
            {
                Icon = IconConstants.Module.Overtime,
                Color = "warning",
                CssClass = "badge-ot"
            },
            RequestModule.Trip => new UIStyle
            {
                Icon = IconConstants.Module.Trip,
                Color = "primary",
                CssClass = "badge-trip"
            },
            RequestModule.Attendance => new UIStyle
            {
                Icon = IconConstants.Module.Attendance,
                Color = "success",
                CssClass = "badge-attendance"
            },
            RequestModule.Equipment => new UIStyle
            {
                Icon = IconConstants.Module.Equipment,
                Color = "secondary",
                CssClass = "badge-equipment"
            },
            RequestModule.Payroll => new UIStyle
            {
                Icon = IconConstants.Module.Payroll,
                Color = "success",
                CssClass = "badge-payroll"
            },
            RequestModule.AccessChange => new UIStyle
            {
                Icon = IconConstants.Module.AccessChange,
                Color = "warning",
                CssClass = "badge-access-change"
            },
            RequestModule.Endpoint => new UIStyle
            {
                Icon = IconConstants.Module.Endpoint,
                Color = "info",
                CssClass = "badge-endpoint"
            },
            _ => new UIStyle
            {
                Icon = IconConstants.Module.Default,
                Color = "default",
                CssClass = "badge-default"
            }
        };
    }
}
