using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Core.Constants;

namespace FVN_REGISTER.Shared.Services.Help;

/// <summary>Controls Help visibility using the user's effective function grants, not a single legacy role label.</summary>
public static class HelpAccessPolicy
{
    private static readonly IReadOnlyDictionary<string, int[]> RequiredAny = new Dictionary<string, int[]>(StringComparer.Ordinal)
    {
        ["HOME_WORKSPACE"] = new[] { SecurityFunctionCodes.DashboardView },
        ["WORK_CALENDAR"] = new[] { SecurityFunctionCodes.CalendarView, SecurityFunctionCodes.WorkCalendarView },
        ["LEAVE"] = new[] { SecurityFunctionCodes.LeaveView, SecurityFunctionCodes.LeaveCreate },
        ["OT"] = new[] { SecurityFunctionCodes.OTView, SecurityFunctionCodes.OTCreate },
        ["TRIP"] = new[] { SecurityFunctionCodes.TripView, SecurityFunctionCodes.TripCreate },
        ["APPROVAL_INBOX"] = new[] { SecurityFunctionCodes.LeaveApprove, SecurityFunctionCodes.OTApprove, SecurityFunctionCodes.TripApprove, SecurityFunctionCodes.EquipmentApprove, SecurityFunctionCodes.ExecutionReview },
        ["EXECUTION"] = new[] { SecurityFunctionCodes.ExecutionReview },
        ["EQUIPMENT_WORKSPACE"] = new[] { SecurityFunctionCodes.EquipmentView, SecurityFunctionCodes.EquipmentCreate, SecurityFunctionCodes.EquipmentManage },
        ["EQUIPMENT_CHECKLIST"] = new[] { SecurityFunctionCodes.EquipmentInspectionManage, SecurityFunctionCodes.EquipmentInspectionExecute, SecurityFunctionCodes.EquipmentInspectionApprove, SecurityFunctionCodes.EquipmentInspectionReport },
        ["EQUIPMENT_REPAIR"] = new[] { SecurityFunctionCodes.EquipmentRepair, SecurityFunctionCodes.EquipmentView },
        ["EQUIPMENT_HANDOVER"] = new[] { SecurityFunctionCodes.EquipmentAssign, SecurityFunctionCodes.EquipmentTransfer, SecurityFunctionCodes.EquipmentReturn },
        ["EQUIPMENT_IMPORT"] = new[] { SecurityFunctionCodes.EquipmentImport },
        ["EQUIPMENT_SCHEMA"] = new[] { SecurityFunctionCodes.EquipmentManage },
        ["PUBLIC_INFORMATION_CMS"] = new[] { SecurityFunctionCodes.PublicInformationManage },
        ["PUBLIC_FORMS"] = new[] { SecurityFunctionCodes.PublicFormView, SecurityFunctionCodes.PublicFormManage, SecurityFunctionCodes.PublicFormSubmissionView },
        ["SECURITY_CENTER"] = new[] { SecurityFunctionCodes.SecurityView, SecurityFunctionCodes.SecurityManageRoles, SecurityFunctionCodes.SecurityManageFunctions, SecurityFunctionCodes.SecurityAudit },
        ["ENDPOINT_MANAGEMENT"] = new[] { SecurityFunctionCodes.EndpointView, SecurityFunctionCodes.EndpointInventoryView, SecurityFunctionCodes.EndpointComplianceView },
        ["ADMIN_ORGANIZATION"] = new[] { SecurityFunctionCodes.DepartmentView, SecurityFunctionCodes.DepartmentManage, SecurityFunctionCodes.EmployeeView, SecurityFunctionCodes.EmployeeManage, SecurityFunctionCodes.ApproverView, SecurityFunctionCodes.ApproverManage },
        ["ADMIN_POLICIES"] = new[] { SecurityFunctionCodes.ApprovalPolicyManage, SecurityFunctionCodes.ExecutionPolicyManage, SecurityFunctionCodes.OTLimitManage, SecurityFunctionCodes.HrmUserRoleRuleManage, SecurityFunctionCodes.LeaveTypeManage },
        ["HRM_ATTENDANCE_CALCULATION"] = new[] { SecurityFunctionCodes.AttendanceCalculate, SecurityFunctionCodes.AttendanceView, SecurityFunctionCodes.AttendanceExport },
        ["ADMIN_WORK_CALENDAR"] = new[] { SecurityFunctionCodes.WorkCalendarManage },
        ["ADMIN_ATTENDANCE_SYMBOL_RULES"] = new[] { SecurityFunctionCodes.WorkCalendarSymbolRuleManage },
        ["ADMIN_HRM_SYNC"] = new[] { SecurityFunctionCodes.HrmSyncViewStatus, SecurityFunctionCodes.HrmSyncSync, SecurityFunctionCodes.HrmSyncReview, SecurityFunctionCodes.HrmSyncRetry },
        ["ADMIN_HRM_ATTENDANCE_JOBS"] = new[] { SecurityFunctionCodes.AttendanceCalculate, SecurityFunctionCodes.HrmSyncViewStatus },
        ["ADMIN_OT_REGISTRARS"] = new[] { SecurityFunctionCodes.OTRegistrarManage },
        ["ADMIN_SYSTEM"] = new[] { SecurityFunctionCodes.EmailQueueManage, SecurityFunctionCodes.EmailTemplateManage, SecurityFunctionCodes.BackgroundJobView, SecurityFunctionCodes.BackgroundJobManage, SecurityFunctionCodes.LanguageView, SecurityFunctionCodes.LanguageManage },
        ["REPORTS"] = new[] { SecurityFunctionCodes.PayrollView, SecurityFunctionCodes.AttendanceView, SecurityFunctionCodes.LeaveView, SecurityFunctionCodes.OTView, SecurityFunctionCodes.TripView },
        ["PAYROLL_TABLE"] = new[] { SecurityFunctionCodes.PayrollView, SecurityFunctionCodes.PayrollPrepare, SecurityFunctionCodes.PayrollExport },
        ["HISTORY"] = new[] { SecurityFunctionCodes.LeaveView, SecurityFunctionCodes.OTView, SecurityFunctionCodes.TripView, SecurityFunctionCodes.EquipmentHistory },
        ["PROFILE"] = Array.Empty<int>()
    };

    public static bool CanView(PermissionSnapshotDto? permissions, string? featureCode)
    {
        if (permissions is null || string.IsNullOrWhiteSpace(featureCode) || !RequiredAny.TryGetValue(featureCode, out var required)) return false;
        return required.Length == 0 || required.Any(permissions.Has);
    }

    public static IReadOnlyCollection<string> VisibleFeatures(PermissionSnapshotDto? permissions) =>
        permissions is null ? Array.Empty<string>() : RequiredAny.Keys.Where(code => CanView(permissions, code)).ToArray();
}