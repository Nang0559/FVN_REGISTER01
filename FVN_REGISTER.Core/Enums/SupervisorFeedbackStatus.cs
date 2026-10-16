namespace FVN_REGISTER.Core.Enums;

/// <summary>Single lifecycle for a calendar '?' supervisor request.</summary>
public enum SupervisorFeedbackStatus
{
    None = 0,
    PendingSupervisor = 1,
    Rejected = 2,
    RegisteredPendingApprove = 3,
    Completed = 4
}
