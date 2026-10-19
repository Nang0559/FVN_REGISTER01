namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>
/// Quy tắc màu cho loại HRM LNLMa=0002 (DayType=COMPANY_HOLIDAY):
/// Chủ nhật dùng Navy; ngày nghỉ từ thứ Hai đến thứ Bảy dùng LightYellow.
/// Các loại HRM khác giữ màu cấu hình riêng.
/// </summary>
public static class CalendarHolidayColorPolicy
{
    public static string? Resolve(DateTime date, string? dayType, string? configuredHolidayColor, string? sundayColor)
    {
        var configured = CalendarColor.Normalize(configuredHolidayColor);
        var sunday = CalendarColor.Normalize(sundayColor) ?? "Navy";

        if (string.Equals(dayType?.Trim(), "COMPANY_HOLIDAY", StringComparison.OrdinalIgnoreCase))
            return date.DayOfWeek == DayOfWeek.Sunday ? "Navy" : "LightYellow";

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return configured ?? sunday;

        return configured;
    }
}
