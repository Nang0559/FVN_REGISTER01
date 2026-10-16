using FVN_REGISTER.Application.Policies;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class ExecutionReviewPolicyTests
{
    [Theory]
    [InlineData("Approved", "Approved")]
    [InlineData("Rejected", "Rejected")]
    [InlineData("NeedMoreEvidence", "NeedMoreEvidence")]
    [InlineData("NEED_MORE_EVIDENCE", "NeedMoreEvidence")]
    public void Evidence_review_status_is_normalized(string input, string expected)
        => Assert.Equal(expected, ExecutionReviewPolicy.NormalizeEvidenceReviewStatus(input));

    [Fact]
    public void Evidence_review_maps_to_distinct_confirmation_status()
    {
        Assert.Equal("Rejected", ExecutionReviewPolicy.ConfirmationStatusAfterEvidenceReview("Rejected"));
        Assert.Equal("NeedMoreEvidence", ExecutionReviewPolicy.ConfirmationStatusAfterEvidenceReview("NeedMoreEvidence"));
        Assert.Equal("Pending", ExecutionReviewPolicy.ConfirmationStatusAfterEvidenceReview("Approved"));
    }

    [Theory]
    [InlineData("Rejected", null, true, true, true)]
    [InlineData("Rejected", "  ", true, false, true)]
    [InlineData("Rejected", null, false, true, false)]
    [InlineData("NeedMoreEvidence", "", false, true, true)]
    [InlineData("NeedMoreEvidence", null, true, false, false)]
    [InlineData("Approved", null, true, true, false)]
    [InlineData("Rejected", "Sai giờ", true, true, false)]
    public void Hr_comment_follows_policy_flags(string status, string? note, bool onReject, bool onNeedMore, bool expectError)
        => Assert.Equal(expectError, ExecutionReviewPolicy.ValidateHrComment(status, note, onReject, onNeedMore) is not null);

    [Theory]
    [InlineData("AwaitingConfirmation", null, "unanswered")]
    [InlineData("AwaitingConfirmation", "Pending", "pending")]
    [InlineData("AwaitingConfirmation", "NeedMoreEvidence", "hr-request")]
    [InlineData("AwaitingConfirmation", "Rejected", "rejected")]
    [InlineData("Resolved", "Approved", "complete")]
    public void Calendar_feedback_status_is_mapped(string recon, string? confirmation, string expectedTone)
    {
        var s = ExecutionFeedbackStatusPolicy.Resolve(recon, confirmation, DateTime.Now.AddHours(-2), null);
        if (expectedTone == "unanswered") Assert.Null(s);
        else Assert.Equal(expectedTone, s!.Value.Tone);
    }

    [Fact]
    public void Pending_confirmation_reviewed_after_submit_is_complete()
    {
        var submitted = new DateTime(2026, 10, 1, 8, 0, 0);
        var s = ExecutionFeedbackStatusPolicy.Resolve("AwaitingConfirmation", "Pending", submitted, submitted.AddHours(1));
        Assert.Equal("complete", s!.Value.Tone);
        Assert.Equal("!", s.Value.Marker);

        var resubmitted = ExecutionFeedbackStatusPolicy.Resolve("AwaitingConfirmation", "Pending", submitted.AddHours(2), submitted.AddHours(1));
        Assert.Equal("pending", resubmitted!.Value.Tone);
        Assert.Equal("-", resubmitted.Value.Marker);
    }

    [Fact]
    public void HR_resolution_does_not_overwrite_employee_decision()
    {
        Assert.Equal(
            "REJECTED",
            ExecutionReviewPolicy.PreserveEmployeeDecision("REJECTED"));

        Assert.Null(ExecutionReviewPolicy.PreserveEmployeeDecision(null));
    }
}
