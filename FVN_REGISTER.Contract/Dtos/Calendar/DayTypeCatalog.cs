namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>Một loại ngày dùng chung cho lịch, rule ký hiệu, xuất chấm công/OT.</summary>
public sealed record DayTypeInfo(string Code, byte? HolidayType, string HolidayTypeCode, string Label, string ShortLabel, bool IsHoliday, string? Symbol = null);

/// <summary>
/// Danh mục DayType dùng chung, ánh xạ theo ký hiệu HRM (DM_Loainghile.LNLKyHieu), không theo số LNLMa:
/// CT = Chủ nhật/Nghỉ công ty (COMPANY_HOLIDAY, Navy); L = Ngày lễ (NATIONAL_HOLIDAY, HotPink); NT = Ngày nghỉ thường (OTHER, LightYellow).
/// </summary>
public static class DayTypeCatalog
{
    public static readonly IReadOnlyList<DayTypeInfo> All = new DayTypeInfo[]
    {
        new("NORMAL", null, "NORMAL", "Ngày thường (T2-T7)", "ngày thường", false),
        // HRM CT: Chủ nhật/Nghỉ công ty (Navy); dùng rule CT-.
        new("COMPANY_HOLIDAY", 1, "COMPANY", "Chủ nhật/Nghỉ công ty (CT)", "nghỉ công ty", true, "CT"),
        // Loại legacy: Chủ nhật đã gộp vào COMPANY_HOLIDAY (CT) từ migration 90.
        new("SUNDAY", null, "SUNDAY", "Nghỉ Chủ nhật (CN)", "nghỉ Chủ nhật", true, "CN"),
        // HRM L: Ngày lễ (HotPink).
        new("NATIONAL_HOLIDAY", 2, "NATIONAL", "Nghỉ lễ (L)", "nghỉ lễ", true, "L"),
        // HRM NT: Ngày nghỉ thường (LightYellow), gồm ngày nghỉ riêng của nhân viên (tblNgayNghiLeNhanVien).
        new("OTHER", 4, "OTHER", "Ngày nghỉ thường (NT)", "nghỉ thường", true, "NT"),
    };

    public static string[] Codes => All.Select(x => x.Code).ToArray();

    public static DayTypeInfo FromHolidayType(byte? holidayType)
        => All.FirstOrDefault(x => x.HolidayType == holidayType) ?? All.First(x => x.Code == "COMPANY_HOLIDAY");

    public static string HolidayTypeCode(byte? holidayType) => FromHolidayType(holidayType).HolidayTypeCode;
    public static string HolidayTypeLabel(byte? holidayType) => FromHolidayType(holidayType).Label;
    public static string HolidayTypeShortLabel(byte? holidayType) => FromHolidayType(holidayType).ShortLabel;
}
