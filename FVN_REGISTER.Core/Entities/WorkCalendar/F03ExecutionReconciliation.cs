using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.WorkCalendar;

[Table("F03ExecutionReconciliations")]
public sealed class F03ExecutionReconciliation : BaseAuditEntity
{
    public new long Id { get; set; }

    [Required, StringLength(50)]
    public string ModuleCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string SourceId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string SourceType { get; set; } = "MODULE";

    [StringLength(100)]
    public string? ParticipantId { get; set; }

    public int EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }

    [StringLength(50)]
    public string? PlannedState { get; set; }

    [StringLength(50)]
    public string? ActualState { get; set; }

    [Required, StringLength(50)]
    public string ReconciliationStatus { get; set; } = "None";

    public bool RequiresConfirmation { get; set; }
    public bool RequiresEvidence { get; set; }

    public long? ConfirmationId { get; set; }
    public Guid? ActionId { get; set; }
    public int? ResolutionPolicyId { get; set; }
    public int? ResolutionPolicyVersion { get; set; }
    public string? ResolutionPolicySnapshotJson { get; set; }

    [StringLength(40)]
    public string EmployeeDecisionStatus { get; set; } = "Pending";

    public DateTime? EmployeeDecisionAt { get; set; }
    public int? EmployeeDecisionBy { get; set; }

    [StringLength(2000)]
    public string? EmployeeDecisionComment { get; set; }

    public byte AppealRound { get; set; }

    public DateTime? FinalizedAt { get; set; }
    public int? FinalizedBy { get; set; }

    public string? DetailJson { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedBy { get; set; }
}