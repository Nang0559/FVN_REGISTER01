namespace FVN_REGISTER.Contract.Dtos.Execution;

public sealed record ExecutionResolutionPolicyDto(
    int Id,
    string ModuleCode,
    string PolicyName,
    int PolicyVersion,
    byte ReconciliationMode,
    byte ConfirmationMode,
    byte EvidenceMode,
    byte ReviewMode,
    int? DueHours,
    byte AutoResolveMode,
    byte CorrectionMode,
    int EmployeeResponseHours,
    byte EmployeeTimeoutMode,
    int HrReviewHours,
    bool AllowEmployeeAppeal,
    byte MaxAppealRounds,
    int AppealReviewHours,
    bool RequireEvidenceOnAppeal,
    bool RequireFinalDecision,
    string? FinalDecisionPositionCode,
    byte PayrollCutoffMode,
    bool AllowReopenAfterPayroll,
    byte AdjustmentPeriodMode,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive);

public sealed class ExecutionResolutionPolicyRequest
{
    public string ModuleCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public byte ReconciliationMode { get; set; } = 1;
    public byte ConfirmationMode { get; set; }
    public byte EvidenceMode { get; set; } = 2;
    public byte ReviewMode { get; set; } = 1;
    public int? DueHours { get; set; }
    public byte AutoResolveMode { get; set; }
    public byte CorrectionMode { get; set; }
    public int EmployeeResponseHours { get; set; } = 48;
    public byte EmployeeTimeoutMode { get; set; } = 0;
    public int HrReviewHours { get; set; } = 48;
    public bool AllowEmployeeAppeal { get; set; } = true;
    public byte MaxAppealRounds { get; set; } = 1;
    public int AppealReviewHours { get; set; } = 48;
    public bool RequireEvidenceOnAppeal { get; set; } = true;
    public bool RequireFinalDecision { get; set; } = true;
    public string? FinalDecisionPositionCode { get; set; }
    public byte PayrollCutoffMode { get; set; }
    public bool AllowReopenAfterPayroll { get; set; }
    public byte AdjustmentPeriodMode { get; set; } = 1;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}