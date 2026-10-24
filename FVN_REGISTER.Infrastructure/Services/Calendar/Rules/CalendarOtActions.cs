using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

/// <summary>
/// Nguồn duy nhất cho các nút của dấu "?" liên quan OT:
/// - không có giờ công / sự cố chấm công  -> "Phản hồi HR"
/// - có giờ OT nhưng chưa có đơn OT        -> registrar: "Đăng ký OT"; người khác: "Phản hồi cấp trên về OT" (CalendarSupervisorFeedback)
/// </summary>
internal static class CalendarOtActions
{
    public static CalendarActionOptionDto HrFeedback(string description, string? detailRoute = null, long? reconciliationId = null) => new()
    {
        Code = "OPEN_HR_FEEDBACK",
        Title = "Phản hồi HR",
        Description = description,
        Kind = "FORM",
        DetailRoute = detailRoute,
        ReconciliationId = reconciliationId,
        RequiresReason = true,
        RequiresAttachment = true
    };

    public static CalendarActionOptionDto RegisterOt(DateOnly date, string description) => new()
    {
        Code = "OPEN_OT",
        Title = "Đăng ký OT",
        Description = description,
        Kind = "NAVIGATION",
        DetailRoute = $"/ot/create?date={date:yyyy-MM-dd}"
    };
}
