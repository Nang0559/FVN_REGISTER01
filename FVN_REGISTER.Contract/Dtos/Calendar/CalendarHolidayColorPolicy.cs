namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>
/// Quy tắc màu lịch cho loại HRM có ký hiệu CT (Chủ nhật/Nghỉ công ty):
/// Chủ nhật dùng Navy; ngày nghỉ công ty từ thứ Hai đến thứ Bảy dùng LightYellow.
/// Các loại nghỉ khác giữ màu cấu hình từ HRM.
/// </summary>
public static class CalendarHolidayColorPolicy
{
    public static string? Resolve(DateTime date, string? holidaySymbol, string? configuredHolidayColor, string? sundayColor)
    {
        var configured = CalendarColor.Normalize(configuredHolidayColor);
        var sunday = CalendarColor.Normalize(sundayColor) ?? "Navy";

        if (string.Equals(holidaySymbol?.Trim(), "CT", StringComparison.OrdinalIgnoreCase))
            return date.DayOfWeek == DayOfWeek.Sunday ? configured ?? sunday : "LightYellow";

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return configured ?? sunday;

        return configured;
    }
}
