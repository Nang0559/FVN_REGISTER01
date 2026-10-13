using FVN_REGISTER.Application.Policies;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class ExecutionFeedbackStatusPolicyTests
{
    private static readonly DateTime Submitted = new(2026, 10, 13, 9, 0, 0);

    [Fact]
    public void No_confirmation_keeps_legacy_marker()
        => Assert.Null(ExecutionFeedbackStatusPolicy.Resolve("AwaitingConfirmation", null, null, null));

    [Fact]
    public void Submitted_and_not_reviewed_is_pending()
    {
        var fb = ExecutionFeedbackStatusPolicy.Resolve("AwaitingConfirmation", "Pending", Submitted, null);
        Assert.Equal(ExecutionFeedbackStatusPolicy.Pending, fb!.Value.Tone);
        Assert.Equal("Pending", fb.Value.Label);
    }

    [Fact]
    public void Hr_review_after_submit_is_complete()
    {
        var fb = ExecutionFeedbackStatusPolicy.Resolve("AwaitingConfirmation", "Pending", Submitted, Submitted.AddMinutes(5));
        Assert.Equal(ExecutionFeedbackStatusPolicy.Complete, fb!.Value.Tone);
        Assert.False(fb.Value.RequiresEmployeeAction);
    }

    [Fact]
    public void Need_more_evidence_is_hr_request_and_needs_employee()
    {
        var fb = ExecutionFeedbackStatusPolicy.Resolve("AwaitingConfirmation", "NeedMoreEvidence", Submitted, Submitted.AddMinutes(5));
        Assert.Equal(ExecutionFeedbackStatusPolicy.HrRequest, fb!.Value.Tone);
        Assert.True(fb.Value.RequiresEmployeeAction);
    }

    [Fact]
    public void Rejected_wins_over_resolved_reconciliation_status()
    {
        var fb = ExecutionFeedbackStatusPolicy.Resolve("Resolved", "Rejected", Submitted, Submitted.AddMinutes(5));
        Assert.Equal(ExecutionFeedbackStatusPolicy.Rejected, fb!.Value.Tone);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void Hr_resolution_awaiting_employee_is_hr_resolved_and_needs_employee(string hrDecision)
    {
        var fb = ExecutionFeedbackStatusPolicy.Resolve("AwaitingEmployeeDecision", hrDecision, Submitted, Submitted.AddMinutes(5));
        Assert.Equal(ExecutionFeedbackStatusPolicy.HrResolved, fb!.Value.Tone);
        Assert.True(fb.Value.RequiresEmployeeAction);
    }

    [Theory]
    [InlineData("AppealReviewing")]
    [InlineData("FinalDecisionPending")]
    public void Appeal_in_review_is_pending_even_if_confirmation_was_rejected(string reconciliationStatus)
    {
        var fb = ExecutionFeedbackStatusPolicy.Resolve(reconciliationStatus, "Rejected", Submitted, Submitted.AddMinutes(5));
        Assert.Equal(ExecutionFeedbackStatusPolicy.Pending, fb!.Value.Tone);
        Assert.False(fb.Value.RequiresEmployeeAction);
    }
}
