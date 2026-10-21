using Zeitwerk.Web.Domain;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class GapsTests
{
    private static readonly DateTimeOffset DayStart = T("00:00"), DayEnd = T("22.10 00:00");

    private static TimeEntry E(int project, string start, string? end) =>
        new() { ProjectId = project, Start = T(start), End = end is null ? null : T(end) };

    private static (DateTimeOffset, DateTimeOffset)[] Find(params TimeEntry[] entries) =>
        Gaps.Find(entries, DayStart, DayEnd).Select(g => (g.Start, g.End)).ToArray();

    [Fact]
    public void No_entries_no_gaps() => Assert.Empty(Find());

    [Fact]
    public void Adjacent_entries_have_no_gap() =>
        Assert.Empty(Find(E(A, "09:00", "10:00"), E(B, "10:00", "11:00")));

    [Fact]
    public void Gap_between_two_entries_with_neighbours()
    {
        var a = E(A, "09:00", "10:00");
        var b = E(B, "10:30", "11:00");

        var gap = Assert.Single(Gaps.Find([b, a], DayStart, DayEnd));

        Assert.Equal((T("10:00"), T("10:30")), (gap.Start, gap.End));
        Assert.Same(a, gap.Before);
        Assert.Same(b, gap.After);
        Assert.Equal(TimeSpan.FromMinutes(30), gap.Duration);
    }

    [Fact]
    public void Nothing_before_the_first_or_after_the_last_entry() =>
        Assert.Equal(
            [(T("10:00"), T("12:00")), (T("12:30"), T("13:00"))],
            Find(E(A, "09:00", "10:00"), E(B, "12:00", "12:30"), E(A, "13:00", "14:00")));

    [Fact]
    public void Gap_before_running_entry_but_nothing_after_it() =>
        Assert.Equal(
            [(T("10:00"), T("11:00"))],
            Find(E(A, "09:00", "10:00"), E(B, "11:00", null)));

    [Fact]
    public void On_hold_entry_directly_after_the_last_entry_leaves_no_gap() =>
        Assert.Empty(Find(E(A, "09:00", "09:50"), E(B, "09:50", null)));

    [Fact]
    public void Gap_before_on_hold_entry() =>
        Assert.Equal(
            [(T("09:50"), T("10:05"))],
            Find(E(A, "09:00", "09:50"), E(B, "10:05", null)));

    [Fact]
    public void Entries_of_other_days_are_ignored() =>
        Assert.Equal(
            [(T("10:00"), T("11:00"))],
            Find(E(A, "20.10 15:00", "20.10 16:00"), E(A, "09:00", "10:00"), E(B, "11:00", "12:00"), E(A, "22.10 08:00", "22.10 09:00")));

    [Fact]
    public void Entries_crossing_the_day_boundaries_are_clipped()
    {
        // Vom Vortag bis 01:00 und ab 23:00 in den Folgetag: Lücke nur dazwischen.
        Assert.Equal(
            [(T("01:00"), T("23:00"))],
            Find(E(A, "20.10 23:00", "01:00"), E(B, "23:00", "22.10 02:00")));
    }

    [Fact]
    public void Entry_ending_at_midnight_does_not_create_gap_to_next_day() =>
        Assert.Empty(Find(E(A, "22:00", "22.10 00:00"), E(B, "22.10 00:00", "22.10 01:00")));

    [Fact]
    public void Overlapping_entries_do_not_produce_negative_gaps() =>
        Assert.Equal(
            [(T("11:00"), T("11:30"))],
            Find(E(A, "09:00", "11:00"), E(B, "10:00", "10:30"), E(C, "11:30", "12:00")));
}
