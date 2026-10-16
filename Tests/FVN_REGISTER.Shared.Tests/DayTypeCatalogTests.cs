using FVN_REGISTER.Contract.Dtos.Calendar;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

public sealed class DayTypeCatalogTests
{
    [Theory]
    [InlineData((byte)1, "COMPANY_HOLIDAY")]
    [InlineData((byte)2, "NATIONAL_HOLIDAY")]
    [InlineData((byte)4, "OTHER")]
    public void HolidayType_maps_to_rule_daytype(byte holidayType, string expectedDayType)
        => Assert.Equal(expectedDayType, DayTypeCatalog.FromHolidayType(holidayType).Code);

    [Fact]
    public void Codes_cover_every_daytype_used_by_rules_and_sql()
        => Assert.Equal(
            new[] { "NORMAL", "COMPANY_HOLIDAY", "NATIONAL_HOLIDAY", "OTHER" },
            DayTypeCatalog.Codes);

    [Fact]
    public void Rule_symbols_persisted_on_the_dto_win_over_the_hardcoded_fallback()
    {
        var day = new CalendarDayDto
        {
            Date = new DateOnly(2026, 9, 2),
            Holiday = new CalendarHolidayInfoDto { Code = "HOL-COMPANY-20260902", Name = "[COMPANY]|Nghỉ công ty", Type = "COMPANY" },
            Shift = new CalendarShiftInfoDto { ShiftAbbr = "C1" },
            Attendance = new CalendarAttendanceInfoDto
            {
                CheckIn = new DateTime(2026, 9, 2, 6, 0, 0),
                CheckOut = new DateTime(2026, 9, 2, 18, 0, 0),
                RuleWorkSymbol = "NL8",
                RuleOtSymbol = "NLK4",
                RuleCode = "CHOL-C1"
            }
        };

        var symbols = AttendanceSymbolCalculator.Resolve(day);
        Assert.Equal("NL8", symbols.WorkSymbol);
        Assert.Equal("NLK4", symbols.OtSymbol);
    }
}
