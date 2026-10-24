using FVN_REGISTER.Core.Attributes;

namespace FVN_REGISTER.Core.Constants;

/// <summary>
/// Approval-only capability codes that are not part of the legacy module constants yet.
/// Kept separate to avoid renumbering or changing existing Leave/OT/Trip/Equipment codes.
/// </summary>
public static class ApprovalSecurityFunctionCodes
{
    // 2810 belongs to Payroll.Approve (SQL/53). PublicForm.View used to share this code and was moved to
    // SecurityFunctionCodes.PublicFormView = 2819. The attribute makes the capability discoverable, otherwise
    // SecurityFunctionRegistryService.ReconcileAsync sees an undeclared key and sets it to PendingRetirement / IsActive = 0.
    [SecurityFunctionDefinition("Payroll.Approve", "Phê duyệt bảng công/bảng lương", ModuleCode = "Payroll", ActionCode = "Approve", ScopeCode = AuthorizationScopeCodes.Department)]
    public const int PayrollApprove = 2810;
}
