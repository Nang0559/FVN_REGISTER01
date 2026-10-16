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
        // T2-T7 (kể cả Thứ 7) là ngày làm việc thường. OT: K.. (hệ số 150%).
        new("NORMAL",           null, "NORMAL",   "Ngày thường (T2-T7)",          "ngày thường",  false),
        // HRM 0002 CT. Chủ nhật mặc định cũng là loại này. OT: CN.. / CNK.. (hệ số 200%).
        new("COMPANY_HOLIDAY",  1,    "COMPANY",  "Chủ nhật / Nghỉ công ty (CT)", "nghỉ công ty", true, "CT"),
        // HRM 0003 L. OT: NL.. / NLK.. (hệ số 300%).
        new("NATIONAL_HOLIDAY", 2,    "NATIONAL", "Ngày lễ (L)",                  "ngày lễ",      true, "L"),
        // HRM 0001 NT: ngày thường được công ty cho nghỉ. OT: T.. / TK.. (hệ số 200%).
        new("OTHER",            4,    "OTHER",    "Ngày nghỉ thường (NT)",        "nghỉ thường",  true, "NT"),
    };

    public static string[] Codes => All.Select(x => x.Code).ToArray();

    public static DayTypeInfo FromHolidayType(byte? holidayType)
        => All.FirstOrDefault(x => x.HolidayType == holidayType) ?? All.First(x => x.Code == "COMPANY_HOLIDAY");

    public static string HolidayTypeCode(byte? holidayType) => FromHolidayType(holidayType).HolidayTypeCode;
    public static string HolidayTypeLabel(byte? holidayType) => FromHolidayType(holidayType).Label;
    public static string HolidayTypeShortLabel(byte? holidayType) => FromHolidayType(holidayType).ShortLabel;
}
