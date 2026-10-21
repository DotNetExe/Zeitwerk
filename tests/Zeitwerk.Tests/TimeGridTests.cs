using Zeitwerk.Web.Domain;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class TimeGridTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("09:00", "09:00")]
    [InlineData("09:03", "09:00")]
    [InlineData("09:04:59", "09:00")]
    [InlineData("09:05", "09:05")]
    public void Floor_rounds_down_to_grid(string at, string expected) =>
        Assert.Equal(T(expected), TimeGrid.Floor(T(at)));

    [Theory]
    [InlineData("09:50", "09:50")]
    [InlineData("09:50:20", "09:50")]
    [InlineData("09:51", "09:50")]
    [InlineData("09:51:01", "09:55")]
    [InlineData("09:47", "09:50")]
    public void Ceil_rounds_up_with_tolerance(string at, string expected) =>
        Assert.Equal(T(expected), TimeGrid.Ceil(T(at), Tolerance));

    [Fact]
    public void Ceil_without_tolerance_is_strict() =>
        Assert.Equal(T("09:55"), TimeGrid.Ceil(T("09:50:01"), TimeSpan.Zero));
}
