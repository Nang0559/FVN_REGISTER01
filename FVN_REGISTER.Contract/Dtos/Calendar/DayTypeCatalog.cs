namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>Một loại ngày dùng chung cho lịch, rule ký hiệu, xuất chấm công/OT.</summary>
public sealed record DayTypeInfo(string Code, byte? HolidayType, string HolidayTypeCode, string Label, string ShortLabel, bool IsHoliday, string? Symbol = null);

/// <summary>
/// Danh mục DayType dùng chung. Mã HRM được chuẩn hóa thành 4 chữ số:
/// 0001 = NORMAL; 0002 = SUNDAY hoặc COMPANY_HOLIDAY tùy ngày thực tế; 0003 = NATIONAL_HOLIDAY.
/// </summary>
public static class DayTypeCatalog
{
    public static readonly IReadOnlyList<DayTypeInfo> All = new DayTypeInfo[]
    {
        new("NORMAL", null, "NORMAL", "Ngày thường (T2-T7)", "ngày thường", false),
        // COMPANY_HOLIDAY là loại legacy; HRM 0001 hiện được chuẩn hóa thành NORMAL.
        new("COMPANY_HOLIDAY", 1, "COMPANY", "Nghỉ công ty (T2-T7)", "nghỉ công ty", true, "NT"),
        // HRM 0002 và Chủ nhật mặc định; dùng rule CN/CNK riêng.
        new("SUNDAY", null, "SUNDAY", "Nghỉ Chủ nhật (CN)", "nghỉ Chủ nhật", true, "CN"),
        // HRM 0003.
        new("NATIONAL_HOLIDAY", 2, "NATIONAL", "Nghỉ lễ (L)", "nghỉ lễ", true, "L"),
        // Loại legacy cho dữ liệu tùy chỉnh cũ, không dùng cho ba mã HRM chuẩn.
        new("OTHER", 4, "OTHER", "Ngày nghỉ khác", "nghỉ khác", true, "NT"),
    };

    public static string[] Codes => All.Select(x => x.Code).ToArray();

    // HolidayType=1 is a broad COMPANY bucket shared by SUNDAY and COMPANY_HOLIDAY.
    // Use the persisted DayType when the caller needs the exact calendar classification.
    public static DayTypeInfo FromHolidayType(byte? holidayType)
        => All.FirstOrDefault(x => x.HolidayType == holidayType) ?? All.First(x => x.Code == "COMPANY_HOLIDAY");

    public static string HolidayTypeCode(byte? holidayType) => FromHolidayType(holidayType).HolidayTypeCode;
    public static string HolidayTypeLabel(byte? holidayType) => FromHolidayType(holidayType).Label;
    public static string HolidayTypeShortLabel(byte? holidayType) => FromHolidayType(holidayType).ShortLabel;
}
