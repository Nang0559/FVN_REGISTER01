using FVN_REGISTER.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Contract.Requests.LateEarly;

public sealed class CreateLateEarlyRequestDto
{
    [Required, StringLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required]
    public LateEarlyType LateEarlyType { get; set; }

    [Required]
    public DateTime WorkDate { get; set; }

    [Required]
    public TimeSpan PlannedTime { get; set; }

    [Required, StringLength(30)]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReasonNote { get; set; }

    public List<ApprovalSelectionDto> ApprovalSelections { get; set; } = new();
}
