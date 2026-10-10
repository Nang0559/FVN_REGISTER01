using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Infrastructure.Services.Calendar.Rules;

/// <summary>
/// One OT per employee per day: any OT registration that is not Rejected/Cancelled already "uses"
/// the day, so calendar rules must not invite a second registration for it.
/// </summary>
internal static class CalendarOtRegistrations
{
    public static CalendarRegistrationDto? FindOpenRequest(CalendarDayDto day) =>
        day.Registrations.FirstOrDefault(x =>
            string.Equals(x.ModuleCode, "OT", StringComparison.OrdinalIgnoreCase)
            && !IsClosed(x.Status)
            && !IsClosed(x.ApprovalStatus));

    public static bool IsClosed(string? status)
    {
        var value = status?.Trim();
        return string.Equals(value, "Rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Canceled", StringComparison.OrdinalIgnoreCase);
    }
}
