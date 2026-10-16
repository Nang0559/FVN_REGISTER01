using FVN_REGISTER.Contract.Dtos.Calendar;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

public sealed class AttendanceSymbolCalculatorTests
{
    private static CalendarDayDto Day(DateOnly date, string shift, DateTime? inTime, DateTime? outTime,
        int actualOt = 0, string? displayValue = null, bool holiday = false) => new()
    {
        Date = date,
        Holiday = holiday ? new CalendarHolidayInfoDto() : null,
        Shift = new CalendarShiftInfoDto { ShiftAbbr = shift },
        Attendance = new CalendarAttendanceInfoDto
        {
            CheckIn = inTime,
            CheckOut = outTime,
            ActualOtMinutes = actualOt,
            DisplayValue = displayValue
        }
    };

    [Fact]
    public void Weekday_C1_With245OtMinutes_ShowsC1AndK4()
    {
        var date = new DateOnly(2026, 9, 9); // Thứ tư
        var day = Day(date, "Ca C1", new DateTime(2026, 9, 9, 5, 50, 0), new DateTime(2026, 9, 9, 18, 5, 0),
            actualOt: 245, displayValue: "C1");

        var symbols = AttendanceSymbolCalculator.Resolve(day);

        Assert.Equal("C1", symbols.WorkSymbol);
        Assert.Equal("K4", symbols.OtSymbol);
    }

    [Fact]
    public void Marker_QuestionMark_DoesNotReplaceWorkSymbol()
    {
        var day = Day(new DateOnly(2026, 9, 9), "Ca C1", new DateTime(2026, 9, 9, 5, 50, 0),
            new DateTime(2026, 9, 9, 18, 5, 0), actualOt: 245, displayValue: "?");

        Assert.Equal("C1", AttendanceSymbolCalculator.Resolve(day).WorkSymbol);
    }

    [Fact]
    public void Weekday_WithoutOt_HasNoOtSymbol()
    {
        var day = Day(new DateOnly(2026, 9, 9), "Ca C1", new DateTime(2026, 9, 9, 5, 50, 0),
            new DateTime(2026, 9, 9, 14, 5, 0));

        Assert.Null(AttendanceSymbolCalculator.Resolve(day).OtSymbol);
    }

    [Fact]
    public void RuleEncodedValues_TakePrecedenceOverCalculation()
    {
        var day = Day(new DateOnly(2026, 9, 9), "Ca C1", new DateTime(2026, 9, 9, 5, 50, 0),
            new DateTime(2026, 9, 9, 18, 5, 0), actualOt: 245,
            displayValue: "@@ATT-SYMBOL@@C2§K3§§§RULE1");

        var symbols = AttendanceSymbolCalculator.Resolve(day);

        Assert.Equal("C2", symbols.WorkSymbol);
        Assert.Equal("K3", symbols.OtSymbol);
    }
}
