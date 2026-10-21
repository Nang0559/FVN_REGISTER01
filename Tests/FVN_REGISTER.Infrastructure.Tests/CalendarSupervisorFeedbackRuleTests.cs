using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure.Services.Calendar;
using FVN_REGISTER.Infrastructure.Services.Calendar.Rules;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class CalendarSupervisorFeedbackRuleTests
{
    private static CalendarDayDto Day(bool mustReport, SupervisorFeedbackStatus status = SupervisorFeedbackStatus.None, string? note = null) => new()
    {
        Date = new DateOnly(2026, 10, 14),
        IsPast = true,
        MustReportOtToSupervisor = mustReport,
        SupervisorFeedbackStatus = (int)status,
        SupervisorFeedbackNote = note,
        Attendance = new CalendarAttendanceInfoDto { HasActualOt = true, ActualOtMinutes = 120 }
    };

    private static CalendarIssueDto Evaluate(CalendarDayDto day, bool isOtRegistrar = false) =>
        Assert.Single(new CalendarDayRuleEngine(new ICalendarDayRule[] { new OtActualWithoutRequestRule() })
            .Evaluate(new CalendarDayRuleContext(day, Array.Empty<CalendarItemDto>(), isOtRegistrar)));

    [Fact]
    public void NonRegistrar_SeesSupervisorFeedback_NotRegisterOt()
    {
        var issue = Evaluate(Day(mustReport: true));
        // Không có quyền đăng ký OT: chỉ được phản hồi cấp trên (không mở đăng ký OT, không gửi thẳng HR).
        Assert.Equal("OPEN_SUPERVISOR_FEEDBACK", Assert.Single(issue.Actions).Code);
    }

    [Fact]
    public void Registrar_StillSeesRegisterOt_NotSupervisorFeedback()
    {
        var issue = Evaluate(Day(mustReport: false), isOtRegistrar: true);
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_OT");
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_HR_FEEDBACK");
        Assert.DoesNotContain(issue.Actions, x => x.Code == "OPEN_SUPERVISOR_FEEDBACK");
    }

    [Fact]
    public void PendingSupervisorFeedback_HidesSendButton_AndShowsStatus()
    {
        var issue = Evaluate(Day(true, SupervisorFeedbackStatus.PendingSupervisor));
        Assert.DoesNotContain(issue.Actions, x => x.Code == "OPEN_SUPERVISOR_FEEDBACK");
        Assert.Contains("chờ cấp trên", issue.Summary);
    }

    [Fact]
    public void RejectedBySupervisor_ShowsRejectionAndReason()
    {
        var issue = Evaluate(Day(true, SupervisorFeedbackStatus.Rejected, "Không có kế hoạch OT"));
        Assert.DoesNotContain(issue.Actions, x => x.Code == "OPEN_SUPERVISOR_FEEDBACK");
        Assert.Contains("Từ chối yêu cầu từ cấp trên", issue.Summary);
        Assert.Contains("Không có kế hoạch OT", issue.Summary);
    }
}
