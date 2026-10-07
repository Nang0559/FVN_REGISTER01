using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Contract.Requests.Approvals;

public sealed class ApprovalPolicyRequest
{
    [Required]
    public int RequestType { get; set; }

    /// <summary>Requester department.</summary>
    public int DeptCode { get; set; }

    /// <summary>Optional requester PositionCode. Null/empty means all positions in the department.</summary>
    [StringLength(20)]
    public string? PositionCode { get; set; }

    /// <summary>Position of the approver selected from F03Positions.</summary>
    [Required, StringLength(20)]
    public string ApprovalPositionCode { get; set; } = string.Empty;

    /// <summary>
    /// Derived by the server from ApprovalPositionCode.DefaultApproveLevel.
    /// Clients no longer need to calculate or edit this value.
    /// </summary>
    public int Level { get; set; }

    [Range(1, 100)]
    public int Sequence { get; set; } = 1;

    /// <summary>Derived display value; retained for backward compatibility with existing clients.</summary>
    [StringLength(100)]
    public string LevelName { get; set; } = string.Empty;

    /// <summary>Derived display value; retained for backward compatibility with existing clients.</summary>
    [StringLength(50)]
    public string RoleName { get; set; } = string.Empty;

    public bool Required { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
