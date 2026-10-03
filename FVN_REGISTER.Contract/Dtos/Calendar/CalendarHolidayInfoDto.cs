namespace FVN_REGISTER.Contract.Dtos.Calendar;

public sealed class CalendarHolidayInfoDto
{
    public string? Code { get; init; }
    public string? Name { get; init; }

    /// <summary>COMPANY, NATIONAL, COMPENSATORY or OTHER.</summary>
    public string? Type
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_type)) return _type;
            var source = Code ?? Name ?? string.Empty;
            if (source.Contains("NATIONAL", StringComparison.OrdinalIgnoreCase)) return "NATIONAL";
            if (source.Contains("COMPENSATORY", StringComparison.OrdinalIgnoreCase)) return "COMPENSATORY";
            if (source.Contains("OTHER", StringComparison.OrdinalIgnoreCase)) return "OTHER";
            if (source.Contains("COMPANY", StringComparison.OrdinalIgnoreCase)) return "COMPANY";
            return null;
        }
        init => _type = value;
    }

    private string? _type;
}
