namespace FVN_REGISTER.Application.Policies;

/// <summary>Hiển thị trạng thái phản hồi (calendar marker + nhãn + tông màu).</summary>
/// <param name="Tone">unanswered | pending | hr-request | hr-resolved | complete | rejected (khớp class CSS fcc-fb-*)</param>
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
    /// <summary>HR đã xử lý (vòng đầu) và đang chờ nhân viên xác nhận/khiếu nại.</summary>
    public const string HrResolved = "hr-resolved";

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

        // HR đã xử lý phản hồi nhưng nhân viên còn phải xác nhận/khiếu nại (reconciliation=AwaitingEmployeeDecision).
        // Lúc này Confirmation.Status đã là Approved/Rejected do HR quyết định, KHÔNG được hiểu là "Complete":
        // nhiệm vụ vẫn mở và lịch phải còn lối vào để nhân viên xem kết quả.
        if (string.Equals(reconciliationStatus, "AwaitingEmployeeDecision", StringComparison.OrdinalIgnoreCase))
            return new(HrResolved, "!", "HR processed", 2, true);

        // Nhân viên đã khiếu nại, đang chờ HR vòng sau: trạng thái Rejected cũ không còn đúng.
        if (string.Equals(reconciliationStatus, "AppealReviewing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reconciliationStatus, "FinalDecisionPending", StringComparison.OrdinalIgnoreCase))
            return new(Pending, "-", "Pending", 1, false);

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
