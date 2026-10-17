namespace FVN_REGISTER.Contract.Dtos.Calendar;

public sealed class CalendarDayDto
{
    public DateOnly Date { get; init; }
    public int? WorkYear { get; init; }
    public bool IsToday { get; init; }
    public bool IsFuture { get; init; }
    public bool IsPast { get; init; }
    public bool IsWeekend { get; init; }
    public bool IsWorkingDay { get; init; }

    public CalendarHolidayInfoDto? Holiday { get; init; }
    public CalendarShiftInfoDto? Shift { get; init; }
    public CalendarAttendanceInfoDto? Attendance { get; init; }

    public IReadOnlyList<CalendarRegistrationDto> Registrations { get; init; } = Array.Empty<CalendarRegistrationDto>();
    public IReadOnlyList<CalendarIssueDto> Issues { get; init; } = Array.Empty<CalendarIssueDto>();

    public bool CanRegisterLeave { get; init; }
    public bool CanRegisterOT { get; init; }
    public bool CanRegisterTrip { get; init; }

    /// <summary>User không có quyền đăng ký OT trực tiếp: ô "?" hiện "Phản hồi cấp trên" thay cho "Đăng ký OT".</summary>
    public bool MustReportOtToSupervisor { get; init; }

    /// <summary>Trạng thái phản hồi cấp trên gần nhất của ngày này (0 = chưa có). Giá trị theo SupervisorFeedbackStatus.</summary>
    public int SupervisorFeedbackStatus { get; init; }
    public string? SupervisorFeedbackNote { get; init; }
    public string? AvailabilityNote { get; init; }

    /// <summary>Màu nền ô lịch theo cấu hình màu loại ngày HRM (đã chuẩn hoá CSS).</summary>
    public string? DayColor { get; init; }
}