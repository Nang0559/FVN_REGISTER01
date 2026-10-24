namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

public readonly record struct AttendanceDateRange(DateOnly From, DateOnly To);

public static class AttendanceDateRanges
{
    /// <summary>Collapse dates into ordered contiguous ranges.</summary>
    public static IReadOnlyList<AttendanceDateRange> FromDates(IEnumerable<DateOnly> dates)
    {
        var result = new List<AttendanceDateRange>();
        DateOnly? start = null;
        DateOnly previous = default;
        foreach (var date in dates.Distinct().OrderBy(x => x))
        {
            if (start is null)
            {
                start = date;
            }
            else if (date != previous.AddDays(1))
            {
                result.Add(new AttendanceDateRange(start.Value, previous));
                start = date;
            }
            previous = date;
        }
        if (start is not null)
            result.Add(new AttendanceDateRange(start.Value, previous));
        return result;
    }
}
