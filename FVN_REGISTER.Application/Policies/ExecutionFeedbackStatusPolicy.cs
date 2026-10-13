namespace FVN_REGISTER.Application.Policies;

/// <summary>Hiển thị trạng thái phản hồi (calendar marker + nhãn + tông màu).</summary>
/// <param name="Tone">unanswered | pending | hr-request | complete | rejected (khớp class CSS fcc-fb-*)</param>
public readonly record struct ExecutionFeedbackStatus(string Tone, string Marker, string Label, byte Severity, bool RequiresEmployeeAction);

/// <summary>
/// Nguồn duy nhất ánh xạ (trạng thái reconciliation + confirmation) sang marker/nhãn trên lịch.
///   "?" chưa phản hồi · "-" Pending (chờ HR) · "?" vàng HR request · "!" xanh + ✓ Complete · "✕" Rejected.
/// </summary>
public static class ExecutionFeedbackStatusPolicy
{
    public const string Unanswered = "unanswered";
    public const string Pending = "pending";
    public const string HrRequest = "hr-request";
    public const string Complete = "complete";
    public const string Rejected = "rejected";

    /// <returns>null khi nhân viên chưa gửi phản hồi (giữ hành vi "?" / "OK" cũ).</returns>
    public static ExecutionFeedbackStatus? Resolve(
        string? reconciliationStatus,
        string? confirmationStatus,
        DateTime? submittedAt,
        DateTime? reviewedAt)
    {
        // Nhân viên chưa gửi phản hồi: giữ hành vi cũ ("?" / "OK" theo ReconciliationStatus).
        if (confirmationStatus is null)
            return null;

        if (string.Equals(confirmationStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            return new(Rejected, "✕", "Rejected", 3, true);

        if (string.Equals(reconciliationStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            return new(Complete, "!", "Complete", 0, false);

        if (string.Equals(confirmationStatus, "NeedMoreEvidence", StringComparison.OrdinalIgnoreCase))
            return new(HrRequest, "?", "HR request", 2, true);

        if (string.Equals(confirmationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            return new(Complete, "!", "Complete", 0, false);

        // Pending: HR đã duyệt xong evidence sau lần gửi gần nhất => Complete, ngược lại chờ HR.
        if (reviewedAt.HasValue && submittedAt.HasValue && reviewedAt.Value >= submittedAt.Value)
            return new(Complete, "!", "Complete", 0, false);

        return new(Pending, "-", "Pending", 1, false);
    }
}
