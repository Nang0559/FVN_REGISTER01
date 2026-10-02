using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.WorkCalendar;

[Table("F03ExecutionPolicies")]
public sealed class F03ExecutionPolicy : BaseAuditEntity
{
    public new int Id { get; set; }

    [Required, StringLength(50)]
    public string ModuleCode { get; set; } = string.Empty;

    [StringLength(200)]
    public string PolicyName { get; set; } = string.Empty;

    public int PolicyVersion { get; set; } = 1;

    public byte ReconciliationMode { get; set; } = 1;
    public byte ConfirmationMode { get; set; }
    public byte EvidenceMode { get; set; } = 2;
    public byte ReviewMode { get; set; } = 1;
    public int? DueHours { get; set; }
    public byte AutoResolveMode { get; set; }
    public byte CorrectionMode { get; set; }

    // Resolution workflow policy. These values govern a reconciliation case
    // after the employee submits feedback; they do not alter payroll directly.
    public int EmployeeResponseHours { get; set; } = 48;
    public int HrReviewHours { get; set; } = 48;
    public bool AllowEmployeeAppeal { get; set; } = true;
    public byte MaxAppealRounds { get; set; } = 1;
    public int AppealReviewHours { get; set; } = 48;
    public bool RequireEvidenceOnAppeal { get; set; } = true;
    public bool RequireFinalDecision { get; set; } = true;

    [StringLength(20)]
    public string? FinalDecisionPositionCode { get; set; }

    // 0 = finalize current internal decision at payroll cutoff;
    // 1 = escalate to final authority before payroll finalization.
    public byte PayrollCutoffMode { get; set; } = 0;

    public bool AllowReopenAfterPayroll { get; set; } = false;
    public byte AdjustmentPeriodMode { get; set; } = 1;

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}