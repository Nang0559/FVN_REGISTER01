namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>Một loại ngày dùng chung cho lịch, rule ký hiệu, xuất chấm công/OT.</summary>
public sealed record DayTypeInfo(string Code, byte? HolidayType, string HolidayTypeCode, string Label, string ShortLabel, bool IsHoliday, string? Symbol = null);

/// <summary>
/// Danh mục DayType DUY NHẤT (thay cho 4 bản viết cứng trước đây: WorkCalendarManagement.razor, WorkCalendarService,
/// CompanyHolidayDto, AttendanceSymbolRules.razor). Giá trị HolidayType 1-4 khớp F03CompanyHolidays.HolidayType;
/// DayType khớp F03AttendanceSymbolRules.DayType và dbo.fn_ResolveDayType (SQL/83).
/// Ngày nghỉ cụ thể không nằm ở đây: chúng được đồng bộ từ HRM (tblNgayNghiLe/DM_Loainghile).
/// </summary>
public static class DayTypeCatalog
{
    public static readonly IReadOnlyList<DayTypeInfo> All = new DayTypeInfo[]
    {
        new("NORMAL",           null, "NORMAL",       "Ngày thường",           "ngày thường",   false),
        new("COMPANY_SATURDAY", null, "SATURDAY",     "Thứ 7 nghỉ công ty",    "thứ 7",         false),
        new("SUNDAY",           null, "SUNDAY",       "Chủ nhật",              "chủ nhật",      false),
        // 3 loại nghỉ theo HRM DM_Loainghile: 0002 CT, 0003 L, 0001 NT. Tên/màu thật lấy từ DB (F03CompanyHolidays.HolidayTypeName/HolidayColor);
        // các nhãn dưới đây chỉ là dự phòng khi dòng ngày nghỉ chưa có tên HRM.
        new("COMPANY_HOLIDAY",  1,    "COMPANY",      "Chủ nhật/Nghỉ công ty", "nghỉ công ty",  true, "CT"),
        new("NATIONAL_HOLIDAY", 2,    "NATIONAL",     "Ngày lễ",               "ngày lễ",       true, "L"),
        new("OTHER",            4,    "OTHER",        "Ngày nghỉ thường",      "nghỉ thường",   true, "NT"),
    };

    public static string[] Codes => All.Select(x => x.Code).ToArray();

    public static DayTypeInfo FromHolidayType(byte? holidayType)
        => All.FirstOrDefault(x => x.HolidayType == holidayType) ?? All.First(x => x.Code == "COMPANY_HOLIDAY");

    public static string HolidayTypeCode(byte? holidayType) => FromHolidayType(holidayType).HolidayTypeCode;
    public static string HolidayTypeLabel(byte? holidayType) => FromHolidayType(holidayType).Label;
    public static string HolidayTypeShortLabel(byte? holidayType) => FromHolidayType(holidayType).ShortLabel;
}
