using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Calendar;

public interface ISharedWorkCalendarService
{
    Task<ServiceResult<CalendarMonthDto>> GetMonthAsync(
        string employeeCode,
        int userId,
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? allowedModules = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<CalendarAlertItemDto>>> GetAlertsAsync(
        string employeeCode,
        int userId,
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? allowedModules = null,
        CancellationToken cancellationToken = default);
}
