using Zeitwerk.Web.Domain;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class DaySplitterTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void R14_stop_after_midnight_splits_entry_at_local_midnight()
    {
        // 21.10.2026 ist Sommerzeit (UTC+2): lokale Mitternacht = 21.10. 22:00 UTC
        var timer = new TimerHarness(Berlin);
        timer.SwitchTo(A, "20:00");
        timer.Stop("22.10 01:02");

        Assert.Equal(["1 20:00-22:00", "1 22:00-22.01:05"], timer.Timeline);
    }

    [Fact]
    public void R14_switch_after_midnight_splits_closed_entry()
    {
        var timer = new TimerHarness(Berlin);
        timer.SwitchTo(A, "21:00");
        timer.SwitchTo(B, "23:47");

        Assert.Equal(["1 21:00-22:00", "1 22:00-23:50", "2 23:50-…"], timer.Timeline);
    }

    [Fact]
    public void Entry_spanning_several_days_gets_one_piece_per_day()
    {
        var entry = new TimeEntry { ProjectId = A, Start = T("21:00"), End = T("23.10 03:00") };

        var pieces = DaySplitter.Split(entry, TimeZoneInfo.Utc);

        Assert.Equal(T("22.10 00:00"), entry.End);
        Assert.Equal(
            [(T("22.10 00:00"), T("23.10 00:00")), (T("23.10 00:00"), T("23.10 03:00"))],
            pieces.Select(p => (p.Start, p.End!.Value)));
    }

    [Fact]
    public void Entry_ending_exactly_at_midnight_is_not_split()
    {
        var entry = new TimeEntry { ProjectId = A, Start = T("21:00"), End = T("22.10 00:00") };

        Assert.Empty(DaySplitter.Split(entry, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Start_of_day_respects_daylight_saving_change()
    {
        // 25.10.2026: Umstellung auf Winterzeit
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero), DaySplitter.StartOfDay(new DateOnly(2026, 10, 25), Berlin));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero), DaySplitter.StartOfDay(new DateOnly(2026, 10, 26), Berlin));
    }
}
