using FVN_REGISTER.Infrastructure.Services.HrmSync;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class AttendanceDateRangesTests
{
    [Fact]
    public void FromDates_collapses_contiguous_dates_and_splits_gaps()
    {
        var dates = new[]
        {
            new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2),
            new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 9)
        };

        var ranges = AttendanceDateRanges.FromDates(dates);

        Assert.Equal(3, ranges.Count);
        Assert.Equal(new AttendanceDateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3)), ranges[0]);
        Assert.Equal(new AttendanceDateRange(new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7)), ranges[1]);
        Assert.Equal(new AttendanceDateRange(new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9)), ranges[2]);
    }

    [Fact]
    public void FromDates_returns_empty_for_no_dates()
        => Assert.Empty(AttendanceDateRanges.FromDates(Array.Empty<DateOnly>()));
}
