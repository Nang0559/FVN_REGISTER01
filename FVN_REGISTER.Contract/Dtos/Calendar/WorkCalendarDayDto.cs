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
    /// <summary>Màu nền ô lịch (đã chuẩn hoá CSS): màu loại nghỉ HRM; Chủ nhật mặc định lấy màu loại CT.</summary>
    public string? DayColor { get; set; }
    public bool CanRegisterLeave { get; set; }
    public bool CanRegisterOT { get; set; }
    public bool CanRegisterTrip { get; set; }
    /// <summary>true khi người xem KHÔNG phải registrar OT của phòng ban nhân viên này: phải báo cấp trên ("Phản hồi cấp trên") thay vì tự đăng ký OT. Không phụ thuộc ngày quá khứ/tương lai.</summary>
    public bool MustReportOtToSupervisor { get; set; }
    public string? AvailabilityNote { get; set; }
    public bool IsFuture { get; set; }
    public bool IsPast { get; set; }
}
