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
}