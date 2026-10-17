using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

public sealed class AttendanceNumericVarianceRule : ICalendarDayRule
{
    public int Order => 10;

    public IReadOnlyList<CalendarIssueDto> Evaluate(CalendarDayRuleContext context)
    {
        var attendance = context.Day.Attendance;
        if (attendance is null || !attendance.IsNumericVariance)
            return Array.Empty<CalendarIssueDto>();

        var attendanceItem = context.SourceItems.FirstOrDefault(x => x.ModuleCode == "ATTENDANCE");

        var hrFeedback = CalendarOtActions.HrFeedback(
            "Báo sự cố/chênh lệch chấm công và có thể bổ sung bằng chứng.",
            attendance.DetailRoute ?? attendanceItem?.DetailRoute);
        var actions = new List<CalendarActionOptionDto>();

        var existingOt = CalendarOtRegistrations.FindOpenRequest(context.Day);
        var hasOtHours = attendance.HasActualOt
            || (attendance.ActualHours.HasValue
                && attendance.RequiredHours.HasValue
                && attendance.ActualHours.Value > attendance.RequiredHours.Value);

        if (existingOt is not null)
        {
            // Mỗi nhân viên 1 đơn OT/ngày: mở đơn hiện có thay vì mời đăng ký trùng.
            if (!string.IsNullOrWhiteSpace(existingOt.DetailRoute))
            {
                actions.Add(new CalendarActionOptionDto
                {
                    Code = "OPEN_OT_REQUEST",
                    Title = "Xem đơn OT",
                    Description = "Ngày này đã có đơn OT; mở đơn hiện có.",
                    Kind = "NAVIGATION",
                    DetailRoute = existingOt.DetailRoute,
                    RequestId = existingOt.RequestId
                });
            }
            actions.Add(hrFeedback);
        }
        else if (hasOtHours)
        {
            // Có giờ OT mà chưa có đơn:
            // - người có quyền đăng ký OT -> "Đăng ký OT" (+ Phản hồi HR)
            // - người khác -> "Phản hồi cấp trên về OT"; khi đã gửi/bị từ chối/đã chuyển HR thì nút ẩn và còn "Phản hồi HR".
            if (context.Day.MustReportOtToSupervisor)
            {
                var supervisorAction = CalendarSupervisorFeedback.BuildAction(context.Day);
                if (supervisorAction is not null) actions.Add(supervisorAction);
                else actions.Add(hrFeedback);
            }
            else
            {
                if (context.IsOtRegistrar)
                    actions.Add(CalendarOtActions.RegisterOt(context.Day.Date, "Thực tế vượt giờ ca; mở màn hình đăng ký OT cho ngày này."));
                actions.Add(hrFeedback);
            }
        }
        else
        {
            // Không có giờ công / sự cố chấm công: phản hồi HR.
            actions.Add(hrFeedback);
        }

        var summary = attendance.ActualHours.HasValue && attendance.RequiredHours.HasValue
            ? $"Thực tế {attendance.ActualHours.Value:0.##} giờ / ca {attendance.RequiredHours.Value:0.##} giờ."
            : $"Giá trị chấm công: {attendance.DisplayValue ?? "chênh lệch"}.";

        return new[]
        {
            new CalendarIssueDto
            {
                Code = "ATTENDANCE_TIME_VARIANCE",
                ModuleCode = "ATTENDANCE",
                Severity = 2,
                Marker = "?",
                Title = "Chênh lệch giờ công",
                Summary = summary + (context.Day.MustReportOtToSupervisor ? CalendarSupervisorFeedback.StatusText(context.Day) : string.Empty),
                SourceId = attendance.SourceId ?? attendanceItem?.SourceId,
                ActionId = attendance.ActionId ?? attendanceItem?.ActionId,
                DetailRoute = attendance.DetailRoute ?? attendanceItem?.DetailRoute,
                Actions = actions
            }
        };
    }
}