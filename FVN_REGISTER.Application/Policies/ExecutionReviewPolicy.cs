namespace FVN_REGISTER.Application.Policies;

public static class ExecutionReviewPolicy
{
    public static string NormalizeEvidenceReviewStatus(string value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "Approved",
            "REJECTED" => "Rejected",
            "NEEDMOREEVIDENCE" => "NeedMoreEvidence",
            "NEED_MORE_EVIDENCE" => "NeedMoreEvidence",
            _ => throw new ArgumentException(
                "ReviewStatus chỉ được là Approved, Rejected hoặc NeedMoreEvidence.")
        };

    /// <summary>
    /// Approved  -> "Pending" (case vẫn mở cho tới khi HR Resolve; lịch suy ra "Complete" từ ReviewedAt).
    /// NeedMoreEvidence -> "NeedMoreEvidence" (HR request).  Rejected -> "Rejected".
    /// </summary>
    public static string ConfirmationStatusAfterEvidenceReview(string reviewStatus)
        => NormalizeEvidenceReviewStatus(reviewStatus) switch
        {
            "Approved" => "Pending",
            "Rejected" => "Rejected",
            _ => "NeedMoreEvidence"
        };

    /// <summary>Kiểm tra ý kiến HR theo cờ policy. Trả về thông báo lỗi hoặc null nếu hợp lệ.</summary>
    public static string? ValidateHrComment(
        string reviewStatus,
        string? note,
        bool requireOnReject,
        bool requireOnNeedMore)
    {
        if (!string.IsNullOrWhiteSpace(note))
            return null;

        return NormalizeEvidenceReviewStatus(reviewStatus) switch
        {
            "Rejected" when requireOnReject => "Chính sách đối soát yêu cầu HR nhập ý kiến khi Từ chối.",
            "NeedMoreEvidence" when requireOnNeedMore => "Chính sách đối soát yêu cầu HR nhập ý kiến khi yêu cầu Cần bổ sung.",
            _ => null
        };
    }

    public static string? PreserveEmployeeDecision(string? employeeDecision)
        => employeeDecision;
}
