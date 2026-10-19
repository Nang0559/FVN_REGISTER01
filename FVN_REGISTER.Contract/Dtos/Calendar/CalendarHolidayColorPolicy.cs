namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>
/// Quy tắc màu cho HRM LNLMa=0002 (DayType=SUNDAY):
/// Chủ nhật dùng Navy; các ngày nghỉ công ty từ thứ Hai đến thứ Bảy dùng LightYellow.
/// LNLMa=0003 (NATIONAL_HOLIDAY) giữ nguyên màu HRM, ví dụ HotPink.
/// Các loại HRM khác giữ màu cấu hình riêng.
/// </summary>
public static class CalendarHolidayColorPolicy
{
    public static string? Resolve(DateTime date, string? dayType, string? configuredHolidayColor, string? sundayColor)
    {
        var configured = CalendarColor.Normalize(configuredHolidayColor);
        var sunday = CalendarColor.Normalize(sundayColor) ?? "Navy";

        if (string.Equals(dayType?.Trim(), "SUNDAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dayType?.Trim(), "COMPANY_HOLIDAY", StringComparison.OrdinalIgnoreCase))
            return date.DayOfWeek == DayOfWeek.Sunday ? "Navy" : "LightYellow";

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return configured ?? sunday;

        return configured;
    }
}
