using FVN_REGISTER.Contract.Dtos.Calendar;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

/// <summary>AttendanceSymbolCalculator chỉ đọc ký hiệu do rule đã ghi, không được tự tính lại.</summary>
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
    public void NoRuleResult_DoesNotInventOtSymbol()
    {
        var day = Day(new DateOnly(2026, 9, 9), "Ca C1", new DateTime(2026, 9, 9, 5, 50, 0),
            new DateTime(2026, 9, 9, 18, 5, 0), actualOt: 245, displayValue: "C1");

        var symbols = AttendanceSymbolCalculator.Resolve(day);

        Assert.Equal("C1", symbols.WorkSymbol); // giá trị HRM
        Assert.Null(symbols.OtSymbol);          // không tự suy ra K4
    }

    [Fact]
    public void OffDayWithoutRuleResult_DoesNotInventNlCnTPrefix()
    {
        var day = Day(new DateOnly(2026, 9, 6), "Ca C1", new DateTime(2026, 9, 6, 6, 0, 0),
            new DateTime(2026, 9, 6, 18, 0, 0), actualOt: 240, displayValue: "CN");

        var symbols = AttendanceSymbolCalculator.Resolve(day);

        Assert.Equal("CN", symbols.WorkSymbol);
        Assert.Null(symbols.OtSymbol);
    }

    [Fact]
    public void RuleEncodedValues_AreReturnedAsIs()
    {
        var day = Day(new DateOnly(2026, 9, 9), "Ca C1", new DateTime(2026, 9, 9, 5, 50, 0),
            new DateTime(2026, 9, 9, 18, 5, 0), actualOt: 245,
            displayValue: "@@ATT-SYMBOL@@C2§K3§§§RULE1");

        var symbols = AttendanceSymbolCalculator.Resolve(day);

        Assert.Equal("C2", symbols.WorkSymbol);
        Assert.Equal("K3", symbols.OtSymbol);
    }

    [Fact]
    public void NoAttendance_ReturnsDefault() =>
        Assert.Equal(default, AttendanceSymbolCalculator.Resolve(new CalendarDayDto { Date = new DateOnly(2026, 9, 9) }));
}
