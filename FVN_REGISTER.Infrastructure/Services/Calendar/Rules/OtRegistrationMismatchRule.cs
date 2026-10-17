using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

/// <summary>
/// Flags an approved OT registration when the official HRM actual OT differs by minutes
/// or the registered pay category differs from the HRM day type. The calendar uses '?'.
/// </summary>
public sealed class OtRegistrationMismatchRule : ICalendarDayRule
{
    public int Order => 45;

    public IReadOnlyList<CalendarIssueDto> Evaluate(CalendarDayRuleContext context)
    {
        var day = context.Day;
        var attendance = day.Attendance;
        if (attendance is null || !attendance.HasActualOt)
            return Array.Empty<CalendarIssueDto>();

        var registration = day.Registrations.FirstOrDefault(x =>
            string.Equals(x.ModuleCode, "Overtime", StringComparison.OrdinalIgnoreCase)
            && (x.IsApproved
                || IsApproved(x.ApprovalStatus)
                || IsApproved(x.Status)));
        if (registration is null || !registration.OTHours.HasValue)
            return Array.Empty<CalendarIssueDto>();

        var actualMinutes = attendance.ActualOtMinutes > 0
            ? attendance.ActualOtMinutes
            : attendance.RecognizedOtMinutes;
        if (actualMinutes <= 0)
            return Array.Empty<CalendarIssueDto>();

        var plannedMinutes = (int)Math.Round(registration.OTHours.Value * 60m, MidpointRounding.AwayFromZero);
        var expectedType = ExpectedOtType(attendance.DayType, day.Date);
        var registeredType = NormalizeOtType(registration.OTTypeCode);
        var typeMismatch = expectedType is not null
            && registeredType is not null
            && !string.Equals(expectedType, registeredType, StringComparison.OrdinalIgnoreCase);
        if (Math.Abs(actualMinutes - plannedMinutes) <= 1 && !typeMismatch)
            return Array.Empty<CalendarIssueDto>();

        var source = context.SourceItems.FirstOrDefault(x =>
            string.Equals(x.ModuleCode, "Overtime", StringComparison.OrdinalIgnoreCase));
        var actions = new List<CalendarActionOptionDto>();
        if (!string.IsNullOrWhiteSpace(registration.DetailRoute))
        {
            actions.Add(new CalendarActionOptionDto
            {
                Code = "OPEN_OT_REQUEST",
                Title = "Xem đơn OT",
                Description = "Mở đơn OT đã duyệt để kiểm tra số giờ và loại OT.",
                Kind = "NAVIGATION",
                DetailRoute = registration.DetailRoute,
                RequestId = registration.RequestId
            });
        }
        actions.Add(new CalendarActionOptionDto
        {
            Code = "OPEN_HR_FEEDBACK",
            Title = "Báo HR đối soát",
            Description = "Gửi HR kiểm tra chênh lệch giữa đăng ký OT và chấm công HRM.",
            Kind = "FORM",
            DetailRoute = source?.DetailRoute,
            RequiresReason = true,
            RequiresAttachment = true
        });

        var differences = new List<string>
        {
            $"Đăng ký {plannedMinutes} phút",
            $"thực tế HRM {actualMinutes} phút"
        };
        if (typeMismatch)
            differences.Add($"loại đăng ký {registeredType}, loại theo HRM {expectedType}");

        return new[]
        {
            new CalendarIssueDto
            {
                Code = "OT_REGISTRATION_MISMATCH",
                ModuleCode = "Overtime",
                Severity = 3,
                Marker = "?",
                Title = "OT đăng ký không khớp thực tế",
                Summary = string.Join("; ", differences) + ". Cần đối soát trước khi ghi nhận công/OT.",
                SourceId = registration.RequestId?.ToString() ?? $"{registration.ModuleCode}:{registration.Start:yyyyMMdd}",
                ActionId = source?.ActionId,
                DetailRoute = source?.DetailRoute ?? registration.DetailRoute,
                RequestId = registration.RequestId,
                Actions = actions
            }
        };
    }

    private static bool IsApproved(string? status) =>
        string.Equals(status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase);

    private static string? ExpectedOtType(string? dayType, DateOnly date) =>
        dayType?.Trim().ToUpperInvariant() switch
        {
            "NATIONAL_HOLIDAY" => "HOL",
            "COMPANY_HOLIDAY" or "SUNDAY" or "OTHER" => "RESTDAY",
            "NORMAL" => "WD",
            _ => date.DayOfWeek == DayOfWeek.Sunday ? "RESTDAY" : null
        };

    private static string? NormalizeOtType(string? code) =>
        code?.Trim().ToUpperInvariant() switch
        {
            "SAT" or "SUN" or "REST" or "RESTDAY" => "RESTDAY",
            "HOL" or "HOLIDAY" => "HOL",
            "WD" or "WEEKDAY" => "WD",
            _ => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant()
        };
}
