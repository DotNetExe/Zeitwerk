using Zeitwerk.Web.Domain;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class EntryRulesTests
{
    private static readonly TimeEntry[] Existing =
    [
        new() { ProjectId = A, Start = T("09:00"), End = T("10:00") },
        new() { ProjectId = B, Start = T("14:00") }, // offen
    ];

    [Theory]
    [InlineData("10:00", "11:00")]
    [InlineData("08:00", "09:00")]
    [InlineData("12:00", "14:00")]
    public void Adjacent_or_free_ranges_are_valid(string start, string end) =>
        Assert.Null(EntryRules.Validate(T(start), T(end), Existing));

    [Theory]
    [InlineData("09:55", "10:30")]
    [InlineData("08:00", "11:00")]
    [InlineData("13:00", "14:05")]
    [InlineData("15:00", "16:00")]
    public void Overlapping_ranges_are_rejected(string start, string end) =>
        Assert.NotNull(EntryRules.Validate(T(start), T(end), Existing));

    [Fact]
    public void Off_grid_times_are_rejected() =>
        Assert.NotNull(EntryRules.Validate(T("10:03"), T("11:00"), Existing));

    [Fact]
    public void End_before_start_is_rejected() =>
        Assert.NotNull(EntryRules.Validate(T("11:00"), T("10:00"), Existing));

    [Fact]
    public void Open_entry_may_not_start_before_existing_end() =>
        Assert.NotNull(EntryRules.Validate(T("09:30"), null, Existing.Take(1)));

    [Fact]
    public void Suggested_color_is_least_used()
    {
        var used = ProjectColors.Palette.Take(3).Append(ProjectColors.Palette[0]);

        Assert.Equal(ProjectColors.Palette[3], ProjectColors.Suggest(used));
    }
}
