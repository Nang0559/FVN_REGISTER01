using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

public sealed class OtActualWithoutRequestRule : ICalendarDayRule
{
    public int Order => 50;

    public IReadOnlyList<CalendarIssueDto> Evaluate(CalendarDayRuleContext context)
    {
        if (context.Day.Attendance?.HasActualOt != true)
            return Array.Empty<CalendarIssueDto>();

        if (context.Day.Registrations.Any(x =>
                x.ModuleCode == "OT" && IsApproved(x.Status)))
            return Array.Empty<CalendarIssueDto>();

        // Đã có đơn OT nhưng còn đang chờ duyệt: OT thực tế vẫn chưa được ghi nhận (giữ cảnh báo),
        // nhưng KHÔNG phải "chưa có đơn" và không mời đăng ký trùng.
        var pending = CalendarOtRegistrations.FindOpenRequest(context.Day);

        var source = context.SourceItems.FirstOrDefault(x => x.ModuleCode == "OT" && x.RequiresAction);
        long? reconciliationId = long.TryParse(source?.SourceId, out var parsedReconciliationId)
            ? parsedReconciliationId
            : null;

        var actions = new List<CalendarActionOptionDto>
        {
            new CalendarActionOptionDto
            {
                Code = "OPEN_HR_FEEDBACK",
                Title = "Phản hồi nhân sự",
                Description = "Báo cho nhân sự về OT thực tế chưa có đăng ký.",
                Kind = "FORM",
                DetailRoute = source?.DetailRoute,
                ReconciliationId = reconciliationId,
                RequiresReason = true,
                RequiresAttachment = true
            }
        };

        if (pending is null)
        {
            actions.Add(new CalendarActionOptionDto
            {
                Code = "OPEN_OT",
                Title = "Đăng ký OT",
                Description = "Mở đăng ký OT cho ngày này.",
                Kind = "NAVIGATION",
                DetailRoute = $"/ot/create?date={context.Day.Date:yyyy-MM-dd}"
            });
        }
        else if (!string.IsNullOrWhiteSpace(pending.DetailRoute))
        {
            actions.Add(new CalendarActionOptionDto
            {
                Code = "OPEN_OT_REQUEST",
                Title = "Xem đơn OT",
                Description = "Mở đơn OT đang chờ duyệt của ngày này.",
                Kind = "NAVIGATION",
                DetailRoute = pending.DetailRoute,
                RequestId = pending.RequestId
            });
        }

        var minutes = context.Day.Attendance.ActualOtMinutes;
        var approver = string.IsNullOrWhiteSpace(pending?.CurrentApproverName)
            ? string.Empty
            : $" ({(string.IsNullOrWhiteSpace(pending!.ApprovalLevelName) ? "Người duyệt" : pending.ApprovalLevelName)}: {pending.CurrentApproverName})";

        return new[]
        {
            new CalendarIssueDto
            {
                Code = "OT_ACTUAL_WITHOUT_REQUEST",
                ModuleCode = "OT",
                Severity = (byte)(pending is null ? 3 : 2),
                Marker = "?",
                Title = pending is null
                    ? "Có OT thực tế nhưng chưa có đơn OT"
                    : "Có OT thực tế, đơn OT đang chờ duyệt",
                Summary = pending is null
                    ? $"Phát hiện {minutes} phút OT thực tế nhưng chưa có đăng ký OT được duyệt."
                    : $"Phát hiện {minutes} phút OT thực tế. Đơn OT đã đăng ký đang chờ duyệt{approver}; OT sẽ được ghi nhận sau khi đơn được duyệt.",
                SourceId = source?.SourceId ?? context.Day.Attendance.SourceId,
                ActionId = source?.ActionId,
                DetailRoute = source?.DetailRoute,
                Actions = actions
            }
        };
    }

    private static bool IsApproved(string? status) =>
        string.Equals(status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase);
}