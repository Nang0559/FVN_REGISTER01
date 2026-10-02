namespace FVN_REGISTER.Contract.Dtos.Calendar;

public sealed class CalendarHolidayInfoDto
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    /// <summary>COMPANY, NATIONAL, COMPENSATORY or OTHER.</summary>
    public string? Type { get; init; }
}
