using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Dtos.Registration;

public sealed class SupervisorFeedbackDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int? DepartmentCode { get; set; }
    public DateTime WorkDate { get; set; }
    public DateTime? ActualIn { get; set; }
    public DateTime? ActualOut { get; set; }
    public SupervisorFeedbackStatus Status { get; set; }
    public int? ApproverId { get; set; }
    public string? Note { get; set; }
}

public sealed class SupervisorFeedbackCreateDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public string? Note { get; set; }
}

public sealed class SupervisorFeedbackDecisionDto
{
    public string? Note { get; set; }
    public string? OTTypeCode { get; set; }
    public List<ApprovalSelectionDto>? ApprovalSelections { get; set; }
}
