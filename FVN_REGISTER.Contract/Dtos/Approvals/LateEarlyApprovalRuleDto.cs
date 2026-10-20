namespace FVN_REGISTER.Contract.Dtos.Approvals;

public sealed class LateEarlyApprovalRuleDto
{
    public int DeptCode { get; set; }
    /// <summary>0 = unlimited. Counts active requests in the same calendar month.</summary>
    public int MonthlyLimit { get; set; }
    public bool RequireLevel2ForRetroactive { get; set; } = true;
    /// <summary>0 = disabled. Require level 2 when early-leave time is at least this many minutes before the HRM shift end.</summary>
    public int EarlyLeaveLevel2BeforeMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFallback { get; set; }
}
