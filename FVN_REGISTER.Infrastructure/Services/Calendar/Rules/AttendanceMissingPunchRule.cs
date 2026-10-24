using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

/// <summary>
/// Ngày làm việc đã qua nhưng thiếu giờ vào và/hoặc giờ ra -> dấu "?" đỏ + nút "Phản hồi HR".
/// Trước đây dấu "?" của ngày thiếu giờ ra chỉ xuất hiện "ké" qua rule OT (có phút OT mà thiếu In/Out);
/// khi OT thực tế bị bỏ qua nếu thiếu giờ vào/ra thì ngày đó không còn rule nào sinh issue.
/// Rule này là nguồn riêng cho sự cố chấm công thiếu In/Out, không phụ thuộc OT hay bản ghi đối soát.
/// </summary>
public sealed class AttendanceMissingPunchRule : ICalendarDayRule
{
    public int Order => 5;

    public IReadOnlyList<CalendarIssueDto> Evaluate(CalendarDayRuleContext context)
    {
        var day = context.Day;
        var attendance = day.Attendance;
        if (attendance is null || !day.IsPast)
            return Array.Empty<CalendarIssueDto>();

        var hasIn = attendance.CheckIn.HasValue;
        var hasOut = attendance.CheckOut.HasValue;
        if (hasIn && hasOut)
            return Array.Empty<CalendarIssueDto>();

        // Ngày không có ca làm bắt buộc (nghỉ, lễ, CN/CT không đăng ký) thì không thiếu công.
        if (attendance.RequiredMinutes <= 0)
            return Array.Empty<CalendarIssueDto>();

        if (day.Holiday is not null || IsHolidayDayType(attendance.DayType))
            return Array.Empty<CalendarIssueDto>();

        // Nghỉ phép / công tác cả ngày đã duyệt: không bắt buộc có giờ vào/ra.
        if (day.Registrations.Any(x =>
                (x.ModuleCode == "LEAVE" || x.ModuleCode == "TRIP")
                && !x.IsHalfDay
                && string.Equals(x.Status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase)))
            return Array.Empty<CalendarIssueDto>();

        var attendanceItem = context.SourceItems.FirstOrDefault(x => x.ModuleCode == "ATTENDANCE");
        var summary = !hasIn && !hasOut
            ? "Thiếu giờ vào và giờ ra."
            : !hasIn ? "Thiếu giờ vào." : "Thiếu giờ ra.";

        return new[]
        {
            new CalendarIssueDto
            {
                Code = "ATTENDANCE_MISSING_PUNCH",
                ModuleCode = "ATTENDANCE",
                Severity = 3,
                Marker = "?",
                Title = "Thiếu giờ chấm công",
                Summary = summary,
                SourceId = attendance.SourceId ?? attendanceItem?.SourceId,
                ActionId = attendance.ActionId ?? attendanceItem?.ActionId,
                DetailRoute = attendance.DetailRoute ?? attendanceItem?.DetailRoute,
                Actions = new[]
                {
                    CalendarOtActions.HrFeedback(
                        "Báo sự cố thiếu giờ vào/ra cho nhân sự và có thể bổ sung bằng chứng.",
                        attendance.DetailRoute ?? attendanceItem?.DetailRoute)
                }
            }
        };
    }

    private static bool IsHolidayDayType(string? dayType) =>
        string.Equals(dayType, "COMPANY_HOLIDAY", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dayType, "NATIONAL_HOLIDAY", StringComparison.OrdinalIgnoreCase);
}
