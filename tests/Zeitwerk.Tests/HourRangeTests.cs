using Zeitwerk.Web.Domain;

namespace Zeitwerk.Tests;

public class HourRangeTests
{
    [Fact]
    public void Default_is_seven_to_nineteen() =>
        Assert.Equal(new HourRange(7, 19), HourRange.Default);

    [Theory]
    [InlineData(9 * 60, 17 * 60, 7, 19)]
    [InlineData(6 * 60 + 30, 8 * 60, 6, 19)]
    [InlineData(18 * 60, 19 * 60 + 5, 7, 20)]
    [InlineData(22 * 60, 24 * 60, 7, 24)]
    public void Include_extends_to_full_hours(int start, int end, int from, int to) =>
        Assert.Equal(new HourRange(from, to), HourRange.Default.Include(start, end));

    [Fact]
    public void Union_covers_both_ranges() =>
        Assert.Equal(new HourRange(5, 21), new HourRange(5, 19).Union(new HourRange(7, 21)));
}
