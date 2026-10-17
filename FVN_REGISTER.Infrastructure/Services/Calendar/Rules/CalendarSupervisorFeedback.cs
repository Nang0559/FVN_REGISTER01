using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

/// <summary>
/// Ô "?" cho user KHÔNG có quyền đăng ký OT trực tiếp: thay "Đăng ký OT" bằng "Phản hồi cấp trên về OT"
/// và hiển thị trạng thái xử lý của cấp trên (chờ / từ chối / đã đăng ký OT).
/// </summary>
internal static class CalendarSupervisorFeedback
{
    public const string ActionCode = "OPEN_SUPERVISOR_FEEDBACK";

    /// <summary>Action "Phản hồi cấp trên" — chỉ khi chưa có phản hồi đang xử lý / chưa bị từ chối.</summary>
    public static CalendarActionOptionDto? BuildAction(CalendarDayDto day)
    {
        var status = (SupervisorFeedbackStatus)day.SupervisorFeedbackStatus;
        if (status is SupervisorFeedbackStatus.PendingSupervisor
            or SupervisorFeedbackStatus.RegisteredPendingApprove
            or SupervisorFeedbackStatus.Rejected
            or SupervisorFeedbackStatus.ForwardedToHr)
            return null;

        return new CalendarActionOptionDto
        {
            Code = ActionCode,
            Title = "Phản hồi cấp trên về OT",
            Description = "Gửi người được phân quyền đăng ký OT của bộ phận để đăng ký bổ sung, chuyển HR hoặc từ chối.",
            Kind = "FORM",
            RequiresReason = false,
            RequiresAttachment = false
        };
    }

    /// <summary>Câu trạng thái nối vào Summary của issue; rỗng nếu chưa có phản hồi.</summary>
    public static string StatusText(CalendarDayDto day)
    {
        var note = string.IsNullOrWhiteSpace(day.SupervisorFeedbackNote) ? string.Empty : $" Ghi chú: {day.SupervisorFeedbackNote}";
        return (SupervisorFeedbackStatus)day.SupervisorFeedbackStatus switch
        {
            SupervisorFeedbackStatus.PendingSupervisor => " Đã gửi phản hồi cấp trên, đang chờ cấp trên xử lý.",
            SupervisorFeedbackStatus.Rejected => " Từ chối yêu cầu từ cấp trên." + note,
            SupervisorFeedbackStatus.ForwardedToHr => " Cấp trên đã chuyển HR xử lý." + note,
            SupervisorFeedbackStatus.RegisteredPendingApprove => " Cấp trên đã đăng ký OT, chờ cấp duyệt tiếp theo.",
            _ => string.Empty
        };
    }
}
