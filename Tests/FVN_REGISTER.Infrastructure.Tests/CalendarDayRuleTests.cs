using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Infrastructure.Services.Calendar;
using FVN_REGISTER.Infrastructure.Services.Calendar.Rules;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class CalendarDayRuleTests
{

    [Fact]
    public void CalendarEventDateMatcher_CoversSameDayOtInterval()
    {
        var calendarEvent = new WorkCalendarEventDto
        {
            ModuleCode = "Overtime",
            Start = new DateTime(2026, 9, 22, 18, 0, 0),
            End = new DateTime(2026, 9, 22, 20, 0, 0)
        };

        Assert.True(CalendarEventDateMatcher.CoversDate(
            calendarEvent,
            new DateTime(2026, 9, 22)));

        Assert.False(CalendarEventDateMatcher.CoversDate(
            calendarEvent,
            new DateTime(2026, 9, 23)));
    }

    [Fact]
    public void CalendarEventDateMatcher_UsesHalfOpenIntervalAtMidnight()
    {
        var calendarEvent = new WorkCalendarEventDto
        {
            ModuleCode = "Overtime",
            Start = new DateTime(2026, 9, 22, 22, 0, 0),
            End = new DateTime(2026, 9, 23, 0, 0, 0)
        };

        Assert.True(CalendarEventDateMatcher.CoversDate(
            calendarEvent,
            new DateTime(2026, 9, 22)));

        Assert.False(CalendarEventDateMatcher.CoversDate(
            calendarEvent,
            new DateTime(2026, 9, 23)));
    }

    [Fact]
    public void NumericAttendanceVariance_ProducesWarningAndOtAction_WhenActualExceedsShift()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 24),
            IsFuture = true,
            IsPast = false,
            Attendance = new CalendarAttendanceInfoDto
            {
                ActualHours = 10m,
                RequiredHours = 8m,
                WorkMinutes = 600,
                RequiredMinutes = 480,
                IsNumericVariance = true,
                DisplayValue = "10"
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new AttendanceNumericVarianceRule()
        });

        var issues = engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>(),
            isOtRegistrar: true));

        var issue = Assert.Single(issues);
        Assert.Equal("ATTENDANCE_TIME_VARIANCE", issue.Code);
        Assert.Equal("ATTENDANCE", issue.ModuleCode);
        Assert.Equal((byte)2, issue.Severity);
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_OT");
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_HR_FEEDBACK");
    }

    private static CalendarIssueDto EvaluateVariance(CalendarAttendanceInfoDto attendance, bool isOtRegistrar)
    {
        var day = new CalendarDayDto { Date = new DateOnly(2026, 9, 24), IsPast = true, Attendance = attendance, MustReportOtToSupervisor = !isOtRegistrar };
        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new AttendanceNumericVarianceRule() });
        return Assert.Single(engine.Evaluate(new CalendarDayRuleContext(day, Array.Empty<CalendarItemDto>(), isOtRegistrar)));
    }

    [Fact]
    public void NumericAttendanceVariance_NonRegistrarWithOtHours_OnlyOffersSupervisorFeedback()
    {
        var issue = EvaluateVariance(new CalendarAttendanceInfoDto
        {
            ActualHours = 10m, RequiredHours = 8m, IsNumericVariance = true, DisplayValue = "10"
        }, isOtRegistrar: false);

        var action = Assert.Single(issue.Actions);
        Assert.Equal("OPEN_SUPERVISOR_FEEDBACK", action.Code);
        Assert.Equal("Phản hồi cấp trên về OT", action.Title);
    }

    [Fact]
    public void NumericAttendanceVariance_WithoutWorkHours_OnlyOffersHrFeedback()
    {
        foreach (var registrar in new[] { false, true })
        {
            var issue = EvaluateVariance(new CalendarAttendanceInfoDto
            {
                HasActual = false, IsNumericVariance = true, DisplayValue = "?"
            }, registrar);

            var action = Assert.Single(issue.Actions);
            Assert.Equal("OPEN_HR_FEEDBACK", action.Code);
            Assert.Equal("Phản hồi HR", action.Title);
        }
    }

    [Fact]
    public void ActualOtWithoutRequest_NonRegistrar_OnlyOffersSupervisorFeedback()
    {
        var attendance = new CalendarAttendanceInfoDto { HasActualOt = true, ActualOtMinutes = 90 };
        var day = new CalendarDayDto { Date = new DateOnly(2026, 9, 21), Attendance = attendance, MustReportOtToSupervisor = true };
        var registrarDay = new CalendarDayDto { Date = new DateOnly(2026, 9, 21), Attendance = attendance };
        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new OtActualWithoutRequestRule() });

        var issue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(day, Array.Empty<CalendarItemDto>(), isOtRegistrar: false)));
        Assert.Equal("OPEN_SUPERVISOR_FEEDBACK", Assert.Single(issue.Actions).Code);

        var registrarIssue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(registrarDay, Array.Empty<CalendarItemDto>(), isOtRegistrar: true)));
        Assert.Contains(registrarIssue.Actions, x => x.Code == "OPEN_OT");
        Assert.DoesNotContain(registrarIssue.Actions, x => x.Code == "OPEN_SUPERVISOR_FEEDBACK");
    }

    [Fact]
    public void OtNoActual_OffersFeedbackAndCancel_WhenPastDayHasApprovedOtWithoutActual()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 22),
            IsPast = true,
            Attendance = new CalendarAttendanceInfoDto
            {
                HasActual = true,
                HasActualOt = false
            },
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "Overtime",
                    RequestId = 123,
                    Status = "Approved",
                    Title = "OT 18:00-20:00",
                    Start = new DateTime(2026, 9, 22, 18, 0, 0),
                    End = new DateTime(2026, 9, 22, 20, 0, 0)
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new OtNoActualRule()
        });

        var issues = engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>()));

        var issue = Assert.Single(issues);
        Assert.Equal("OT_NO_ACTUAL", issue.Code);
        Assert.Equal("Overtime", issue.ModuleCode);
        Assert.Equal((byte)3, issue.Severity);
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_HR_FEEDBACK");
        Assert.Contains(issue.Actions, x => x.Code == "CANCEL_OT" && x.RequestId == 123);
    }

    [Fact]
    public void OtNoActual_DoesNotTrigger_WhenOtIsPending()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 22),
            IsPast = true,
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "Overtime",
                    RequestId = 123,
                    Status = "Pending",
                    Start = new DateTime(2026, 9, 22, 18, 0, 0),
                    End = new DateTime(2026, 9, 22, 20, 0, 0)
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new OtNoActualRule()
        });

        Assert.Empty(engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>())));
    }

    [Fact]
    public void ActualOtWithoutRequest_Triggers_WhenOnlyUnapprovedOtExists()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 21),
            Attendance = new CalendarAttendanceInfoDto
            {
                HasActualOt = true,
                ActualOtMinutes = 90
            },
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "Overtime",
                    RequestId = 55,
                    Status = "Pending"
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new OtActualWithoutRequestRule()
        });

        var issue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>())));

        Assert.Equal("OT_ACTUAL_WITHOUT_REQUEST", issue.Code);
    }

    [Fact]
    public void OtRegistrationMismatch_ProducesQuestionMarker_WhenHoursDiffer()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 10, 12),
            IsPast = true,
            Attendance = new CalendarAttendanceInfoDto
            {
                HasActualOt = true,
                ActualOtMinutes = 150,
                RuleDayType = "NORMAL"
            },
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "Overtime",
                    RequestId = 321,
                    Status = "Approved",
                    IsApproved = true,
                    OTHours = 2m,
                    OTTypeCode = "WD",
                    Start = new DateTime(2026, 10, 12, 18, 0, 0),
                    End = new DateTime(2026, 10, 12, 20, 0, 0)
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new OtRegistrationMismatchRule() });
        var issue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(day, Array.Empty<CalendarItemDto>())));

        Assert.Equal("OT_REGISTRATION_MISMATCH", issue.Code);
        Assert.Equal("?", issue.Marker);
        Assert.Contains("Đăng ký 120 phút", issue.Summary);
        Assert.Contains("thực tế HRM 150 phút", issue.Summary);
    }

    [Fact]
    public void OtRegistrationMismatch_DoesNotFlag_WhenHoursAndCategoryMatch()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 10, 12),
            Attendance = new CalendarAttendanceInfoDto
            {
                HasActualOt = true,
                ActualOtMinutes = 120,
                RuleDayType = "NORMAL"
            },
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "Overtime",
                    RequestId = 322,
                    Status = "Approved",
                    IsApproved = true,
                    OTHours = 2m,
                    OTTypeCode = "WD"
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new OtRegistrationMismatchRule() });
        Assert.Empty(engine.Evaluate(new CalendarDayRuleContext(day, Array.Empty<CalendarItemDto>())));
    }

    [Fact]
    public void LeaveConflict_ProducesCriticalIssue_ForFullDayP()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 21),
            Attendance = new CalendarAttendanceInfoDto { HasActual = true },
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "LEAVE",
                    Status = "Approved",
                    SubTypeCode = "P",
                    IsHalfDay = false,
                    DayValue = 1m,
                    RequestId = 9,
                    Title = "Phép năm"
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new LeaveAttendanceConflictRule()
        });

        var issues = engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>()));

        var issue = Assert.Single(issues);
        Assert.Equal("LEAVE_HAS_ATTENDANCE", issue.Code);
        Assert.Equal((byte)3, issue.Severity);
    }

    [Fact]
    public void HalfDayLeave_DoesNotProduceFullDayConflict()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 21),
            Attendance = new CalendarAttendanceInfoDto { HasActual = true },
            Registrations = new[]
            {
                new CalendarRegistrationDto
                {
                    ModuleCode = "LEAVE",
                    SubTypeCode = "P",
                    IsHalfDay = true,
                    DayValue = 0.5m
                }
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new LeaveAttendanceConflictRule()
        });

        var issues = engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>()));

        Assert.Empty(issues);
    }

    [Fact]
    public void ActualOtWithoutRequest_ProducesCriticalIssue()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 21),
            Attendance = new CalendarAttendanceInfoDto
            {
                HasActualOt = true,
                ActualOtMinutes = 90
            }
        };

        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[]
        {
            new OtActualWithoutRequestRule()
        });

        var issues = engine.Evaluate(new CalendarDayRuleContext(
            day,
            Array.Empty<CalendarItemDto>()));

        var issue = Assert.Single(issues);
        Assert.Equal("OT_ACTUAL_WITHOUT_REQUEST", issue.Code);
        Assert.Equal((byte)3, issue.Severity);
    }

    private static CalendarDayDto DayWithOt(string status, string? detailRoute = "/ot/detail/55") => new()
    {
        Date = new DateOnly(2026, 9, 24),
        Attendance = new CalendarAttendanceInfoDto
        {
            HasActualOt = true,
            ActualOtMinutes = 90,
            ActualHours = 10m,
            RequiredHours = 8m,
            IsNumericVariance = true
        },
        Registrations = new[]
        {
            new CalendarRegistrationDto
            {
                ModuleCode = "Overtime",
                RequestId = 55,
                Status = status,
                DetailRoute = detailRoute
            }
        }
    };

    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Escalated")]
    [InlineData("Draft")]
    [InlineData("NeedsRevision")]
    public void ActualOtWithoutRequest_HidesRegisterButton_WhenOtAlreadyRequested(string status)
    {
        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new OtActualWithoutRequestRule() });

        var issue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(
            DayWithOt(status), Array.Empty<CalendarItemDto>())));

        Assert.Equal((byte)2, issue.Severity);
        Assert.DoesNotContain(issue.Actions, x => x.Code == "OPEN_OT");
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_OT_REQUEST" && x.DetailRoute == "/ot/detail/55");
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public void ActualOtWithoutRequest_StillOffersRegister_WhenOnlyClosedOtExists(string status)
    {
        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new OtActualWithoutRequestRule() });

        var issue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(
            DayWithOt(status), Array.Empty<CalendarItemDto>())));

        Assert.Equal((byte)3, issue.Severity);
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_OT");
    }

    [Fact]
    public void NumericAttendanceVariance_DoesNotOfferSecondOt_WhenOtAlreadyRequested()
    {
        var engine = new CalendarDayRuleEngine(new ICalendarDayRule[] { new AttendanceNumericVarianceRule() });

        var issue = Assert.Single(engine.Evaluate(new CalendarDayRuleContext(
            DayWithOt("Pending"), Array.Empty<CalendarItemDto>())));

        Assert.DoesNotContain(issue.Actions, x => x.Code == "OPEN_OT");
        Assert.Contains(issue.Actions, x => x.Code == "OPEN_OT_REQUEST");
    }
}
