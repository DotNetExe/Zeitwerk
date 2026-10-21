using Zeitwerk.Web.Components.Shared;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Tests;

public class TabStatusTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    private static TimeEntry Open(string color = "#4FC3F7") => new()
    {
        Start = Start,
        Project = new Project { Name = "Projekt", Color = color, Customer = new Customer { Name = "Kunde" } },
    };

    private static TabStatus? For(TimeEntry? open, DateTimeOffset now) => TabStatus.For(open, now, t => t.UtcDateTime);

    [Fact]
    public void Idle_has_no_status()
    {
        Assert.Null(For(null, Start));
    }

    [Fact]
    public void Running_shows_duration_in_minutes_and_project()
    {
        var status = For(Open(), Start.AddMinutes(85).AddSeconds(59));

        Assert.Equal("● 1:25 · Kunde / Projekt – Zeitwerk", status!.Title);
        Assert.StartsWith("data:image/svg+xml,", status.Icon);
        Assert.Contains(Uri.EscapeDataString("#4FC3F7"), status.Icon);
    }

    [Fact]
    public void On_hold_shows_start_time()
    {
        var status = For(Open(), Start.AddMinutes(-3));

        Assert.Equal("⏸ ab 08:00 · Kunde / Projekt – Zeitwerk", status!.Title);
    }

    [Fact]
    public void Status_is_equal_within_the_same_minute()
    {
        var open = Open();

        Assert.Equal(For(open, Start.AddSeconds(61)), For(open, Start.AddSeconds(119)));
        Assert.NotEqual(For(open, Start.AddSeconds(119)), For(open, Start.AddSeconds(120)));
    }

    [Fact]
    public void Invalid_color_falls_back_to_palette()
    {
        var status = For(Open("red\"/><script>"), Start.AddMinutes(1));

        Assert.Contains(Uri.EscapeDataString(ProjectColors.Palette[0]), status!.Icon);
        Assert.DoesNotContain("script", status.Icon);
    }
}
