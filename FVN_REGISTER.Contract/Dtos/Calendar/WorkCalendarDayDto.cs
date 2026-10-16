namespace FVN_REGISTER.Contract.Dtos.Calendar;

public sealed class WorkCalendarDayDto
{
    public DateTime Date { get; set; }
    public int? WorkYear { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsWorkingDay { get; set; }
    public string? HolidayCode { get; set; }
    public string? HolidayName { get; set; }
    public string? HolidayType { get; set; }
    /// <summary>Ký hiệu / tên loại / màu của ngày nghỉ theo HRM (L, CT, NT).</summary>
    public string? HolidaySymbol { get; set; }
    public string? HolidayTypeName { get; set; }
    public string? HolidayColor { get; set; }
    public bool CanRegisterLeave { get; set; }
    public bool CanRegisterOT { get; set; }
    public bool CanRegisterTrip { get; set; }
    public string? AvailabilityNote { get; set; }
    public bool IsFuture { get; set; }
    public bool IsPast { get; set; }
}
