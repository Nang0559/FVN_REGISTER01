using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Infrastructure.Services.Calendar;
using FVN_REGISTER.Infrastructure.Services.Calendar.Rules;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class AttendanceMissingPunchRuleTests
{
    private static IReadOnlyList<CalendarIssueDto> Evaluate(CalendarDayDto day)
    {
        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new AttendanceMissingPunchRule() });
        return engine.Evaluate(new CalendarDayRuleContext(day, Array.Empty<CalendarItemDto>()));
    }

    private static CalendarDayDto Day(DateTime? checkIn, DateTime? checkOut, int requiredMinutes = 480, bool isPast = true) => new()
    {
        Date = new DateOnly(2026, 9, 5),
        IsPast = isPast,
        Attendance = new CalendarAttendanceInfoDto { CheckIn = checkIn, CheckOut = checkOut, RequiredMinutes = requiredMinutes }
    };

    [Fact]
    public void MissingCheckOut_PastWorkingDay_ShowsQuestionMarkWithHrFeedback()
    {
        var issue = Assert.Single(Evaluate(Day(new DateTime(2026, 9, 5, 17, 34, 0), null)));

        Assert.Equal("ATTENDANCE_MISSING_PUNCH", issue.Code);
        Assert.Equal("?", issue.Marker);
        Assert.Equal((byte)3, issue.Severity);
        Assert.Equal("OPEN_HR_FEEDBACK", Assert.Single(issue.Actions).Code);
    }

    [Fact]
    public void CompletePunch_NoIssue() =>
        Assert.Empty(Evaluate(Day(new DateTime(2026, 9, 5, 17, 34, 0), new DateTime(2026, 9, 6, 6, 2, 0))));

    [Fact]
    public void Today_Or_Future_NoIssue() =>
        Assert.Empty(Evaluate(Day(new DateTime(2026, 9, 5, 17, 34, 0), null, isPast: false)));

    [Fact]
    public void NoRequiredShift_NoIssue() =>
        Assert.Empty(Evaluate(Day(null, null, requiredMinutes: 0)));
}
