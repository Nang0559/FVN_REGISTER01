using FVN_REGISTER.Core.Attributes;
namespace FVN_REGISTER.Core.Constants;
public static class SecurityFunctionCodes
{
 public const int DashboardView=2701;
 public const int LeaveView=2001,LeaveCreate=2002,LeaveEdit=2003,LeaveCancel=2004,LeaveApprove=2005,LeaveExport=2006;
 public const int OTView=2101,OTCreate=2102,OTEdit=2103,OTCancel=2104,OTApprove=2105,OTReconcile=2106,OTExport=2107;
 public const int TripView=2201,TripCreate=2202,TripEdit=2203,TripCancel=2204,TripApprove=2205,TripExport=2206;
 public const int EquipmentView=2301,EquipmentCreate=2302,EquipmentEdit=2303,EquipmentRepair=2304,EquipmentApprove=2305,EquipmentImport=2306,EquipmentExport=2307,EquipmentCancel=2308,EquipmentAssign=2309,EquipmentTransfer=2310,EquipmentReturn=2311,EquipmentLiquidate=2312,EquipmentQR=2313,EquipmentHistory=2314,EquipmentInspectionManage=2315,EquipmentInspectionExecute=2316,EquipmentInspectionApprove=2317,EquipmentInspectionReport=2318,EquipmentManage=2319,EquipmentFormManage=2320;
 public const int UserManagementView=2401,UserManagementCreate=2402,UserManagementEdit=2403,UserManagementLock=2404,UserManagementResetPassword=2405,UserManagementAssignPermission=2406,UserManagementManageTwoFactor=2407;
 public const int HrmSyncViewStatus=2501,HrmSyncSync=2502,HrmSyncReview=2503,HrmSyncRetry=2504;
 public const int SecurityView=2601,SecurityManageRoles=2602,SecurityManageFunctions=2603,SecurityAudit=2604;
 public static readonly IReadOnlySet<int> SystemCriticalCodes=new HashSet<int>{SecurityView,SecurityManageRoles,SecurityManageFunctions,SecurityAudit};
 public static bool IsSystemCritical(int functionCode)=>SystemCriticalCodes.Contains(functionCode);
 public const int PublicInformationManage=2801,PublicFormManage=2807,PublicFormSubmissionView=2808,PublicFormExport=2809,PublicFormView=2810,PublicFormSubmit=2811,PublicFormFeedback=2812,PublicFormCreate=2813,PublicFormEdit=2814,PublicFormAssignAudience=2815,PublicFormResultView=2816,PublicFormResultExport=2817,PublicFormAuditView=2818,ExecutionReview=2802;
 [SecurityFunctionDefinition("Execution.PolicyManage","Quản lý chính sách đối soát công",ModuleCode="Execution",ActionCode="PolicyManage",ScopeCode=AuthorizationScopeCodes.All)] public const int ExecutionPolicyManage=3073;
 public const int PayrollView=2803,PayrollPrepare=2804,PayrollLock=2805,PayrollExport=2806;
 public const int AttendanceView=2901,AttendanceExport=2902,AttendanceCalculate=2911,AttendanceFeedback=2912;
 public const int DepartmentView=3001,DepartmentManage=3002,EmployeeView=3011,EmployeeManage=3012,LeaveTypeView=3021,LeaveTypeManage=3022,ApproverView=3031,ApproverManage=3032,WorkCalendarView=3041,WorkCalendarManage=3042,CalendarView=3043;
 [SecurityFunctionDefinition("WorkCalendar.SymbolRuleManage","Quản lý rule ký hiệu chấm công",ModuleCode="WorkCalendar",ActionCode="SymbolRuleManage",ScopeCode=AuthorizationScopeCodes.All)] public const int WorkCalendarSymbolRuleManage=3044;
 public const int DepartmentStatusView=3051,OTLimitManage=3061,ApprovalPolicyManage=3071,HrmUserRoleRuleManage=3072,EmailQueueManage=3081,EmailTemplateManage=3082,SecurityAccessChangeView=3091,SecurityAccessChangeCreate=3092,SecurityAccessChangeApprove=3093,SecurityAccessChangeExecute=3094;
 [SecurityFunctionDefinition("Language.View","Xem quản trị ngôn ngữ",ModuleCode="Language",ActionCode="View",ScopeCode=AuthorizationScopeCodes.All)] public const int LanguageView=3201;
 [SecurityFunctionDefinition("Language.Manage","Quản lý định nghĩa ngôn ngữ",ModuleCode="Language",ActionCode="Manage",ScopeCode=AuthorizationScopeCodes.All)] public const int LanguageManage=3202;
 [SecurityFunctionDefinition("Language.Import","Nhập Excel ngôn ngữ",ModuleCode="Language",ActionCode="Import",ScopeCode=AuthorizationScopeCodes.All)] public const int LanguageImport=3203;
 [SecurityFunctionDefinition("Language.Export","Xuất Excel ngôn ngữ",ModuleCode="Language",ActionCode="Export",ScopeCode=AuthorizationScopeCodes.All)] public const int LanguageExport=3204;
 [SecurityFunctionDefinition("Language.Audit","Rà soát ngôn ngữ",ModuleCode="Language",ActionCode="Audit",ScopeCode=AuthorizationScopeCodes.All)] public const int LanguageAudit=3205;
 [SecurityFunctionDefinition("BackgroundJob.View","Xem lịch chạy background job",ModuleCode="BackgroundJob",ActionCode="View",ScopeCode=AuthorizationScopeCodes.All)] public const int BackgroundJobView=3301;
 [SecurityFunctionDefinition("BackgroundJob.Manage","Quản lý lịch chạy background job",ModuleCode="BackgroundJob",ActionCode="Manage",ScopeCode=AuthorizationScopeCodes.All)] public const int BackgroundJobManage=3302;
 public const int EndpointView=3101,EndpointInventoryView=3102,EndpointComplianceView=3103,EndpointAlertView=3104,EndpointAlertResolve=3105,EndpointCredentialProvision=3106,EndpointCredentialRotate=3107,EndpointCredentialRevoke=3108,EndpointSoftwareCatalogView=3109,EndpointSoftwareCatalogManage=3110,EndpointSoftwarePolicySubmit=3111,EndpointSoftwarePolicyApprove=3112,EndpointSoftwareSecurityReview=3113,EndpointServiceCatalogView=3114,EndpointServiceCatalogManage=3115,EndpointServicePolicySubmit=3116,EndpointServicePolicyApprove=3117,EndpointServiceSecurityReview=3118,EndpointInstallRequestCreate=3119,EndpointInstallRequestView=3120,EndpointInstallRequestApprove=3121,EndpointExceptionCreate=3122,EndpointExceptionView=3123,EndpointExceptionApprove=3124,EndpointLanscopeDeploymentManage=3125,EndpointLanscopeDeploymentView=3126;
}
