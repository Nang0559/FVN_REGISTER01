namespace FVN_REGISTER.Core.Constants;

public static class FeatureOperatorCatalog
{
    public const string ExecutionPolicy = "EXECUTION_POLICY";
    public const string ApprovalPolicy = "APPROVAL_POLICY";
    public const string OtLimitRule = "OT_LIMIT_RULE";
    public const string WorkCalendar = "WORK_CALENDAR";
    public const string HrmUserRoleRule = "HRM_USER_ROLE_RULE";
    public const string PublicInformation = "PUBLIC_INFORMATION";
    public const string PublicForm = "PUBLIC_FORM";
    public const string ExecutionReview = "EXECUTION_REVIEW";
    public const string EquipmentSchema = "EQUIPMENT_SCHEMA";
    public const string EndpointSoftwareCatalog = "ENDPOINT_SOFTWARE_CATALOG";

    private static readonly IReadOnlyDictionary<int, string> ResourceTypes =
        new Dictionary<int, string>
        {
            [SecurityFunctionCodes.ExecutionPolicyManage] = ExecutionPolicy,
            [SecurityFunctionCodes.ApprovalPolicyManage] = ApprovalPolicy,
            [SecurityFunctionCodes.OTLimitManage] = OtLimitRule,
            [SecurityFunctionCodes.WorkCalendarManage] = WorkCalendar,
            [SecurityFunctionCodes.HrmUserRoleRuleManage] = HrmUserRoleRule,
            [SecurityFunctionCodes.PublicInformationManage] = PublicInformation,
            [SecurityFunctionCodes.ExecutionReview] = ExecutionReview,
            [SecurityFunctionCodes.PublicFormManage] = PublicForm,
            [SecurityFunctionCodes.PublicFormSubmissionView] = PublicForm,
            [SecurityFunctionCodes.PublicFormExport] = PublicForm,
            [SecurityFunctionCodes.EquipmentFormManage] = EquipmentSchema,
            [SecurityFunctionCodes.EndpointSoftwareCatalogManage] = EndpointSoftwareCatalog
        };

    public static bool TryGetResourceType(int functionCode, out string resourceType)
        => ResourceTypes.TryGetValue(functionCode, out resourceType!);

    public static bool IsModuleWide(int functionCode)
        => ResourceTypes.ContainsKey(functionCode)
           && functionCode != SecurityFunctionCodes.PublicInformationManage
           && functionCode != SecurityFunctionCodes.PublicFormManage
           && functionCode != SecurityFunctionCodes.PublicFormSubmissionView
           && functionCode != SecurityFunctionCodes.PublicFormExport
           && functionCode != SecurityFunctionCodes.EquipmentFormManage;

    public static IReadOnlyDictionary<int, string> All => ResourceTypes;
}
