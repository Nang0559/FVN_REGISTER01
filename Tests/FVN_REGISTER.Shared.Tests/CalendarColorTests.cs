using FVN_REGISTER.Contract.Dtos.Calendar;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

public sealed class CalendarColorTests
{
    [Theory]
    [InlineData("#ff00ff", "#FF00FF")]
    [InlineData("FF00FF", "#FF00FF")]
    [InlineData("#F0F", "#F0F")]
    [InlineData("FFFF00FF", "#FF00FF")]          // AARRGGBB
    [InlineData("255, 0, 255", "#FF00FF")]
    [InlineData("Color [A=255, R=0, G=0, B=255]", "#0000FF")]
    [InlineData("Color [Magenta]", "Magenta")]
    [InlineData("Magenta", "Magenta")]
    [InlineData("-65281", "#FF00FF")]            // .NET ToArgb() của Magenta
    [InlineData("-16776961", "#0000FF")]         // .NET ToArgb() của Blue
    [InlineData("16711680", "#0000FF")]          // OLE/VB BGR của Blue
    [InlineData("16711935", "#FF00FF")]          // OLE/VB BGR của Magenta
    public void Normalize_accepts_hrm_color_formats(string raw, string expected)
        => Assert.Equal(expected, CalendarColor.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not a color!")]
    [InlineData("url(javascript:alert(1))")]
    public void Normalize_rejects_unknown_values(string? raw)
        => Assert.Null(CalendarColor.Normalize(raw));

    [Fact]
    public void ContrastText_is_dark_on_yellow_and_light_on_blue()
    {
        Assert.Equal("#111111", CalendarColor.ContrastText("#FFFF00"));
        Assert.Equal("#FFFFFF", CalendarColor.ContrastText("#0000FF"));
        Assert.Null(CalendarColor.ContrastText(null));
    }

    // 3 loại nghỉ HRM hiện tại: NT=OrangeRed, CT=Red, L=Crimson -> cả ba đều nền đậm, chữ phải trắng và đồng nhất.
    [Theory]
    [InlineData("OrangeRed")]
    [InlineData("Red")]
    [InlineData("Crimson")]
    [InlineData("#DC143C")]
    public void ContrastText_is_white_on_all_hrm_holiday_colors(string color)
        => Assert.Equal("#FFFFFF", CalendarColor.ContrastText(color));

    [Theory]
    [InlineData("OrangeRed", "OrangeRed")]
    [InlineData("Color [Crimson]", "Crimson")]
    public void Normalize_keeps_hrm_named_colors(string raw, string expected)
        => Assert.Equal(expected, CalendarColor.Normalize(raw));
    [Fact]
    public void Company_rest_type_uses_navy_on_sunday_and_light_yellow_on_other_weekdays()
    {
        Assert.Equal("Navy", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 18), "COMPANY_HOLIDAY", "Navy", "Navy"));
        Assert.Equal("LightYellow", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 19), "COMPANY_HOLIDAY", "Navy", "Navy"));
        Assert.Equal("LightYellow", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 17), "COMPANY_HOLIDAY", "Navy", "Navy"));
    }

    [Fact]
    public void Hrm_type_0002_uses_navy_on_sunday_and_light_yellow_on_weekdays()
    {
        Assert.Equal("Navy", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 18), "SUNDAY", "Navy", "Navy"));
        Assert.Equal("LightYellow", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 19), "SUNDAY", "Navy", "Navy"));
        Assert.Equal("LightYellow", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 17), "SUNDAY", "Navy", "Navy"));
    }

    [Fact]
    public void Sunday_without_holiday_uses_navy_fallback()
    {
        Assert.Equal("Navy", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 18), null, null, null));
    }

    [Fact]
    public void Other_holiday_types_keep_their_hrm_configured_color()
    {
        Assert.Equal("HotPink", CalendarHolidayColorPolicy.Resolve(new DateTime(2026, 10, 19), "NATIONAL_HOLIDAY", "HotPink", "Navy"));
    }

    [Fact]
    public void Normal_attendance_day_never_uses_hrm_catalog_color()
    {
        // HRM 0001 may retain LightYellow in DM_Loainghile, but NORMAL is not a holiday.
        Assert.Null(CalendarHolidayColorPolicy.Resolve(
            new DateTime(2026, 10, 19), "NORMAL", "LightYellow", "Navy"));
    }

}
