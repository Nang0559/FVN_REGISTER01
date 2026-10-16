using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Core.Entities.Security;

[Table("F03EndpointGovernancePolicies")]
public sealed class F03EndpointGovernancePolicy : BaseAuditEntity
{
    [Required, StringLength(100)] public string PolicyCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string PolicyName { get; set; } = string.Empty;
    public EndpointGovernanceItemType ItemType { get; set; }
    public EndpointTargetType TargetType { get; set; }
    public int Version { get; set; }
    public bool IsPublished { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [StringLength(30)] public string WorkflowStatus { get; set; } = "Draft";
    public int? ApprovalRequestId { get; set; }
    public int? ApprovalSnapshotId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public int? SubmittedBy { get; set; }
    public bool ChecklistCompleted { get; set; }
    public DateTime? ChecklistCompletedAtUtc { get; set; }
    public int? ChecklistCompletedBy { get; set; }
    [StringLength(2000)] public string? ChecklistNote { get; set; }
    [StringLength(1000)] public string? Remark { get; set; }
    public ICollection<F03EndpointGovernancePolicyItem> Items { get; set; } = new List<F03EndpointGovernancePolicyItem>();
}

[Table("F03EndpointGovernancePolicyItems")]
public sealed class F03EndpointGovernancePolicyItem : BaseAuditEntity
{
    public int PolicyId { get; set; }
    public EndpointGovernanceItemType ItemType { get; set; }
    [Required, StringLength(255)] public string NormalizedName { get; set; } = string.Empty;
    [StringLength(255)] public string? DisplayName { get; set; }
    [StringLength(255)] public string? Publisher { get; set; }
    [StringLength(100)] public string? VersionConstraint { get; set; }
    [StringLength(2000)] public string? AliasNames { get; set; }
    [StringLength(1000)] public string? Remark { get; set; }
    public bool IsAllowed { get; set; } = true;
}

[Table("F03EndpointGovernanceRequests")]
public sealed class F03EndpointGovernanceRequest : BaseRequestEntity
{
    public EndpointGovernanceRequestType RequestType { get; set; }
    public long? EndpointDeviceId { get; set; }
    public int? PolicyId { get; set; }
    public int? PolicyVersion { get; set; }
    public int? PolicyItemId { get; set; }
    public EndpointGovernanceItemType? PolicyItemType { get; set; }
    [StringLength(255)] public string? ItemName { get; set; }
    [StringLength(255)] public string? Publisher { get; set; }
    [StringLength(100)] public string? RequestedVersion { get; set; }
    [StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [StringLength(30)] public string SecurityReviewStatus { get; set; } = "Pending";
    [StringLength(30)] public string WorkflowStatus { get; set; } = "Draft";
    public int? ApprovalSnapshotId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedBy { get; set; }
}

[Table("F03EndpointComplianceFindings")]
public sealed class F03EndpointComplianceFinding : BaseAuditEntity
{
    public long EndpointDeviceId { get; set; }
    public int? PolicyId { get; set; }
    public int PolicyVersion { get; set; }
    public int? PolicyItemId { get; set; }
    public EndpointGovernanceItemType ItemType { get; set; }
    public long? InventoryItemId { get; set; }
    public EndpointComplianceResult Result { get; set; }
    [Required, StringLength(255)] public string ObservedName { get; set; } = string.Empty;
    [StringLength(100)] public string? ObservedVersion { get; set; }
    [StringLength(50)] public string FindingCode { get; set; } = string.Empty;
    [StringLength(2000)] public string? FindingMessage { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
