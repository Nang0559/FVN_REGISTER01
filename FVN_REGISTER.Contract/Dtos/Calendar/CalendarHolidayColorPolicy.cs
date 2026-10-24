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
        var normalizedDayType = dayType?.Trim();

        // NORMAL is a working day. HRM may still keep a display color on catalog 0001,
        // but that color must never paint an ordinary day merely because attendance exists.
        if (string.Equals(normalizedDayType, "NORMAL", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.Equals(normalizedDayType, "SUNDAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dayType?.Trim(), "COMPANY_HOLIDAY", StringComparison.OrdinalIgnoreCase))
            return date.DayOfWeek == DayOfWeek.Sunday ? "Navy" : "LightYellow";

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return configured ?? sunday;

        return configured;
    }
}
