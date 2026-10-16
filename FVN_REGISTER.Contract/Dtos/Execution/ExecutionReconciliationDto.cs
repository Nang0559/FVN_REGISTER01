namespace FVN_REGISTER.Contract.Dtos.Execution;

public sealed record ExecutionReconciliationDto(
    long Id,
    string ModuleCode,
    string SourceType,
    string SourceId,
    string? ParticipantId,
    int EmployeeId,
    DateOnly WorkDate,
    string? PlannedState,
    string? ActualState,
    string ReconciliationStatus,
    bool RequiresConfirmation,
    bool RequiresEvidence,
    long? ConfirmationId,
    Guid? ActionId)
{
    /// <summary>unanswered | pending | hr-request | hr-resolved | complete | rejected. Null khi nhân viên chưa gửi phản hồi.</summary>
    public string? FeedbackTone { get; init; }
    public string? FeedbackLabel { get; init; }
    /// <summary>Ý kiến HR gần nhất (chỉ có khi HR đã review sau lần gửi gần nhất của nhân viên).</summary>
    public string? FeedbackNote { get; init; }
    /// <summary>true khi nhân viên còn việc phải làm (gửi lại / bổ sung / xác nhận kết quả HR).</summary>
    public bool FeedbackRequiresEmployeeAction { get; init; }
}