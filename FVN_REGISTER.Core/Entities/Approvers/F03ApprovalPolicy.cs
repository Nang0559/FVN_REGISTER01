using FVN_REGISTER.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Approvers;

[Table("F03ApprovalPolicies")]
public sealed class F03ApprovalPolicy : BaseAuditEntity
{
    [Required]
    public RequestModule RequestType { get; set; }

    /// <summary>Mandatory requester department scope.</summary>
    public int DeptCode { get; set; }

    /// <summary>Optional requester position scope. Empty means all positions in DeptCode.</summary>
    [StringLength(20)]
    public string? PositionCode { get; set; }

    /// <summary>HRM position selected as the approval level/approver role.</summary>
    [Required, StringLength(20)]
    public string ApprovalPositionCode { get; set; } = string.Empty;

    public int Level { get; set; }

    public int Sequence { get; set; }

    /// <summary>
    /// Display label for the approval level. Nullable because existing database rows may contain NULL.
    /// The service derives this value from the selected approval position when creating/updating a policy.
    /// </summary>
    [StringLength(100)]
    public string? LevelName { get; set; }

    /// <summary>
    /// Legacy/display role label. Nullable for compatibility with existing database rows.
    /// The service derives this value from the selected approval position.
    /// </summary>
    [StringLength(50)]
    public string? RoleName { get; set; }

    public bool Required { get; set; } = true;
}
