using Zeitwerk.Web.Domain;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class SplitMergeRulesTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static TimeEntry Entry(int project, string start, string? end, string? activity = null) =>
        new() { ProjectId = project, Activity = activity, Start = T(start), End = end is null ? null : T(end) };

    [Fact]
    public void Split_shortens_entry_and_returns_second_part_with_new_work()
    {
        var entry = Entry(A, "09:00", "11:00", "T-1");

        var second = SplitMergeRules.Split(entry, T("10:15"), B, "  T-2 ");

        Assert.Equal(T("10:15"), entry.End);
        Assert.Equal((B, "T-2", T("10:15"), (DateTimeOffset?)T("11:00")), (second.ProjectId, second.Activity, second.Start, second.End));
    }

    [Theory]
    [InlineData("09:00")]
    [InlineData("11:00")]
    [InlineData("08:00")]
    [InlineData("11:30")]
    [InlineData("10:03")]
    public void Split_outside_or_off_grid_is_rejected(string at) =>
        Assert.NotNull(SplitMergeRules.ValidateSplit(Entry(A, "09:00", "11:00"), T(at)));

    [Fact]
    public void Open_entry_cannot_be_split()
    {
        var open = Entry(A, "09:00", null);

        Assert.NotNull(SplitMergeRules.ValidateSplit(open, T("09:30")));
        Assert.Empty(SplitMergeRules.SplitPoints(open));
        Assert.Throws<ArgumentException>(() => SplitMergeRules.Split(open, T("09:30"), A, null));
    }

    [Fact]
    public void Split_points_lie_strictly_inside()
    {
        Assert.Equal([T("09:05"), T("09:10")], SplitMergeRules.SplitPoints(Entry(A, "09:00", "09:15")));
        Assert.Empty(SplitMergeRules.SplitPoints(Entry(A, "09:00", "09:05")));
    }

    [Fact]
    public void Adjacent_entries_with_same_work_can_be_merged()
    {
        var first = Entry(A, "09:00", "10:00", "T-1");
        var second = Entry(A, "10:00", "10:30", "T-1");

        SplitMergeRules.Merge(first, second, Utc);

        Assert.Equal(T("10:30"), first.End);
    }

    [Theory]
    [InlineData(B, "T-1", "10:00", "10:30")] // anderes Projekt
    [InlineData(A, "T-2", "10:00", "10:30")] // andere Tätigkeit
    [InlineData(A, null, "10:00", "10:30")]  // keine Tätigkeit
    [InlineData(A, "T-1", "10:05", "10:30")] // Lücke
    [InlineData(A, "T-1", "10:00", null)]    // offen
    public void Non_matching_entries_cannot_be_merged(int project, string? activity, string start, string? end) =>
        Assert.False(SplitMergeRules.CanMerge(Entry(A, "09:00", "10:00", "T-1"), Entry(project, start, end, activity), Utc));

    [Fact]
    public void Order_matters_for_merge() =>
        Assert.False(SplitMergeRules.CanMerge(Entry(A, "10:00", "11:00"), Entry(A, "09:00", "10:00"), Utc));

    [Fact]
    public void Entries_split_at_midnight_are_not_merged() =>
        Assert.False(SplitMergeRules.CanMerge(Entry(A, "23:00", "22.10 00:00"), Entry(A, "22.10 00:00", "22.10 01:00"), Utc));

    [Fact]
    public void Entry_ending_at_midnight_can_absorb_its_predecessor() =>
        Assert.True(SplitMergeRules.CanMerge(Entry(A, "22:00", "23:00"), Entry(A, "23:00", "22.10 00:00"), Utc));

    [Fact]
    public void Mergeable_pairs_are_found_regardless_of_order()
    {
        var a1 = Entry(A, "09:00", "10:00");
        var a2 = Entry(A, "10:00", "11:00");
        var b = Entry(B, "11:00", "12:00");
        var a3 = Entry(A, "12:00", "13:00");

        var pairs = SplitMergeRules.FindMergeablePairs([a3, b, a2, a1], Utc);

        Assert.Equal([(a1, a2)], pairs);
    }

    [Fact]
    public void Merge_all_collapses_chains()
    {
        var a1 = Entry(A, "09:00", "10:00");
        var a2 = Entry(A, "10:00", "10:30");
        var a3 = Entry(A, "10:30", "11:00");
        var b1 = Entry(B, "11:00", "12:00", "X");
        var b2 = Entry(B, "12:00", "12:15", "X");
        var c = Entry(C, "13:00", "14:00");

        var removed = SplitMergeRules.MergeAll([c, b2, a3, b1, a2, a1], Utc);

        Assert.Equal([a2, a3, b2], removed);
        Assert.Equal(T("11:00"), a1.End);
        Assert.Equal(T("12:15"), b1.End);
        Assert.Equal(T("14:00"), c.End);
    }

    [Fact]
    public void Split_then_merge_restores_entry()
    {
        var entry = Entry(A, "09:00", "11:00", "T-1");
        var second = SplitMergeRules.Split(entry, T("10:00"), A, "T-1");

        SplitMergeRules.Merge(entry, second, Utc);

        Assert.Equal(T("11:00"), entry.End);
    }
}
