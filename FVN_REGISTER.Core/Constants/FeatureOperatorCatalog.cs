namespace FVN_REGISTER.Core.Constants;
/// <param name="AssignmentGrantsCapability">
/// When true the (module-wide) assignment itself grants the capability: effective permission =
/// role grant OR active assignment. When false (default) the legacy rule applies: RBAC AND assignment.
/// Only module-wide, non-approve/export capabilities should opt in.
/// </param>
public sealed record FeatureOperatorDefinition(int FunctionCode,string ResourceType,bool ResourceScoped,bool AssignmentGrantsCapability=false);
public static class FeatureOperatorCatalog
{
 public const string Equipment="EQUIPMENT"; public const string ExecutionPolicy="EXECUTION_POLICY"; public const string ApprovalPolicy="APPROVAL_POLICY"; public const string OtLimitRule="OT_LIMIT_RULE"; public const string WorkCalendar="WORK_CALENDAR"; public const string AttendanceSymbolRule="ATTENDANCE_SYMBOL_RULE"; public const string HrmUserRoleRule="HRM_USER_ROLE_RULE"; public const string PublicInformation="PUBLIC_INFORMATION"; public const string PublicForm="PUBLIC_FORM"; public const string ExecutionReview="EXECUTION_REVIEW"; public const string EquipmentSchema="EQUIPMENT_SCHEMA"; public const string EndpointSoftwareCatalog="ENDPOINT_SOFTWARE_CATALOG";
 private static readonly IReadOnlyDictionary<int,FeatureOperatorDefinition> Definitions=new Dictionary<int,FeatureOperatorDefinition>
 {
  // Equipment operator assignment is module-wide; RBAC is still required independently.
  [SecurityFunctionCodes.EquipmentView]=new(SecurityFunctionCodes.EquipmentView,Equipment,false),
  [SecurityFunctionCodes.EquipmentImport]=new(SecurityFunctionCodes.EquipmentImport,Equipment,false),
  [SecurityFunctionCodes.ExecutionPolicyManage]=new(SecurityFunctionCodes.ExecutionPolicyManage,ExecutionPolicy,false),
  [SecurityFunctionCodes.ApprovalPolicyManage]=new(SecurityFunctionCodes.ApprovalPolicyManage,ApprovalPolicy,false),
  [SecurityFunctionCodes.OTLimitManage]=new(SecurityFunctionCodes.OTLimitManage,OtLimitRule,false),
  [SecurityFunctionCodes.WorkCalendarManage]=new(SecurityFunctionCodes.WorkCalendarManage,WorkCalendar,false),
  [SecurityFunctionCodes.WorkCalendarSymbolRuleManage]=new(SecurityFunctionCodes.WorkCalendarSymbolRuleManage,AttendanceSymbolRule,false),
  [SecurityFunctionCodes.HrmUserRoleRuleManage]=new(SecurityFunctionCodes.HrmUserRoleRuleManage,HrmUserRoleRule,false),
  [SecurityFunctionCodes.PublicInformationManage]=new(SecurityFunctionCodes.PublicInformationManage,PublicInformation,true),
  [SecurityFunctionCodes.ExecutionReview]=new(SecurityFunctionCodes.ExecutionReview,ExecutionReview,false,AssignmentGrantsCapability:true),
  [SecurityFunctionCodes.PublicFormManage]=new(SecurityFunctionCodes.PublicFormManage,PublicForm,true),
  [SecurityFunctionCodes.PublicFormSubmissionView]=new(SecurityFunctionCodes.PublicFormSubmissionView,PublicForm,true),
  [SecurityFunctionCodes.PublicFormExport]=new(SecurityFunctionCodes.PublicFormExport,PublicForm,true),
  [SecurityFunctionCodes.EquipmentFormManage]=new(SecurityFunctionCodes.EquipmentFormManage,EquipmentSchema,true),
  [SecurityFunctionCodes.EndpointSoftwareCatalogManage]=new(SecurityFunctionCodes.EndpointSoftwareCatalogManage,EndpointSoftwareCatalog,false)
 };
 public static bool TryGet(int functionCode,out FeatureOperatorDefinition definition)=>Definitions.TryGetValue(functionCode,out definition!);
 public static bool TryGetResourceType(int functionCode,out string resourceType){if(Definitions.TryGetValue(functionCode,out var definition)){resourceType=definition.ResourceType;return true;}resourceType=string.Empty;return false;}
 public static bool AssignmentGrantsCapability(int functionCode)=>Definitions.TryGetValue(functionCode,out var definition)&&definition.AssignmentGrantsCapability&&!definition.ResourceScoped;
 /// <summary>Function codes whose module-wide assignment grants the capability (consumed by AuthorizationService).</summary>
 public static IReadOnlyCollection<int> CapabilityGrantingFunctionCodes=>Definitions.Values.Where(x=>x.AssignmentGrantsCapability&&!x.ResourceScoped).Select(x=>x.FunctionCode).ToArray();
 public static bool IsModuleWide(int functionCode)=>Definitions.TryGetValue(functionCode,out var definition)&&!definition.ResourceScoped;
 public static bool IsModuleWideResourceType(string resourceType)=>Definitions.Values.Any(x=>!x.ResourceScoped&&string.Equals(x.ResourceType,resourceType,StringComparison.OrdinalIgnoreCase));
 public static IReadOnlyDictionary<int,string> All=>Definitions.ToDictionary(x=>x.Key,x=>x.Value.ResourceType);
}
