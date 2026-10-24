using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Responses.LateEarly;

public sealed class LateEarlyRequestDto
{
    public int Id { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int? DeptCode { get; set; }
    public LateEarlyType LateEarlyType { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan PlannedTime { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? ReasonNote { get; set; }
    public string RegisteredByEmployeeCode { get; set; } = string.Empty;
    public bool IsOnBehalf { get; set; }
    public bool IsRetroactive { get; set; }
    public ApprovalStatus RequestStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}
