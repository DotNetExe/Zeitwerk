using Zeitwerk.Web.Domain;

namespace Zeitwerk.Tests;

public class ReportBuilderTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly Customer Acme = new() { Id = 1, Name = "Acme" };
    private static readonly Customer Beta = new() { Id = 2, Name = "Beta" };
    private static readonly Project Web = new() { Id = 1, Name = "Web", Color = "#4FC3F7", CustomerId = 1, Customer = Acme };
    private static readonly Project App = new() { Id = 2, Name = "App", Color = "#81C784", CustomerId = 1, Customer = Acme };
    private static readonly Project Shop = new() { Id = 3, Name = "Shop", Color = "#FFB74D", CustomerId = 2, Customer = Beta };

    /// <summary>Die Woche Mo 19.10. – So 25.10.2026; Vorwoche Mo 12.10. – So 18.10.</summary>
    private static readonly ReportPeriod Week = new(new(2026, 10, 19), new(2026, 10, 26));

    /// <summary>Lokale Berliner Zeit, z. B. "21.10 09:00".</summary>
    private static DateTimeOffset L(string at)
    {
        var parts = at.Split(' ');
        var day = parts[0].Split('.');
        var local = new DateTime(2026, int.Parse(day[1]), int.Parse(day[0])).Add(TimeSpan.Parse(parts[1]));
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Berlin), TimeSpan.Zero);
    }

    private static TimeEntry E(Project project, string start, string? end, string? activity = null) => new()
    {
        ProjectId = project.Id, Project = project, Activity = activity,
        Start = L(start), End = end is null ? null : L(end),
    };

    private static TimeSpan H(double hours) => TimeSpan.FromHours(hours);

    [Fact]
    public void Sums_hours_per_customer_and_project_with_share()
    {
        TimeEntry[] entries =
        [
            E(Web, "19.10 09:00", "19.10 12:00"),
            E(App, "20.10 09:00", "20.10 10:00"),
            E(Shop, "21.10 13:00", "21.10 17:00"),
        ];

        var report = ReportBuilder.Build(entries, Week, Berlin);

        Assert.Equal(H(8), report.Total);
        Assert.Equal([("Acme", H(4), 0.5), ("Beta", H(4), 0.5)],
            report.Customers.OrderBy(r => r.Label).Select(r => (r.Label, r.Duration, r.Share)));
        Assert.Equal([("Shop", "Beta", H(4)), ("Web", "Acme", H(3)), ("App", "Acme", H(1))],
            report.Projects.Select(r => (r.Label, r.Detail!, r.Duration)));
        Assert.Equal("#FFB74D", report.Projects[0].Color);
        Assert.Equal(0.125, report.Projects[2].Share);
    }

    [Fact]
    public void Open_entries_do_not_count()
    {
        TimeEntry[] entries = [E(Web, "19.10 09:00", "19.10 10:00"), E(App, "19.10 10:00", null)];

        var report = ReportBuilder.Build(entries, Week, Berlin);

        Assert.Equal(H(1), report.Total);
        Assert.Single(report.Projects);
    }

    [Fact]
    public void Entries_outside_the_period_do_not_count()
    {
        TimeEntry[] entries = [E(Web, "26.10 09:00", "26.10 10:00"), E(Web, "19.10 00:00", "19.10 01:00")];

        Assert.Equal(H(1), ReportBuilder.Build(entries, Week, Berlin).Total);
    }

    [Fact]
    public void Entry_over_local_midnight_is_split_at_period_boundary()
    {
        // So 25.10. 22:00 bis Mo 26.10. 02:00 – nur die zwei Stunden bis Mitternacht gehören zur Woche.
        TimeEntry[] entries = [E(Web, "25.10 22:00", "26.10 02:00"), E(Web, "18.10 23:00", "19.10 01:00")];

        var report = ReportBuilder.Build(entries, Week, Berlin);

        Assert.Equal(H(3), report.Total); // 1 h am 19.10. + 2 h am 25.10.
        Assert.Equal(H(1), report.PreviousTotal); // 18.10. 23–24 Uhr
    }

    [Fact]
    public void Daylight_saving_change_counts_real_duration()
    {
        // 25.10.2026: Uhr wird um 03:00 auf 02:00 zurückgestellt, 00:00–06:00 sind 7 Stunden.
        TimeEntry[] entries = [E(Web, "25.10 00:00", "25.10 06:00")];

        Assert.Equal(H(7), ReportBuilder.Build(entries, Week, Berlin).Total);
    }

    [Fact]
    public void Compares_with_previous_period()
    {
        TimeEntry[] entries =
        [
            E(Web, "12.10 09:00", "12.10 11:00"),
            E(Shop, "13.10 09:00", "13.10 12:00"),
            E(Web, "19.10 09:00", "19.10 14:00"),
        ];

        var report = ReportBuilder.Build(entries, Week, Berlin);

        Assert.Equal(new ReportPeriod(new(2026, 10, 12), new(2026, 10, 19)), report.PreviousPeriod);
        Assert.Equal(H(5), report.Total);
        Assert.Equal(H(5), report.PreviousTotal);
        Assert.Equal(TimeSpan.Zero, report.Delta);

        var web = report.Projects.Single(r => r.Label == "Web");
        Assert.Equal((H(5), H(2), H(3)), (web.Duration, web.Previous, web.Delta));

        // Nur im Vorzeitraum gebucht: erscheint mit 0 und negativem Delta, ganz unten.
        var shop = report.Projects[^1];
        Assert.Equal(("Shop", TimeSpan.Zero, H(3), H(-3), 0.0), (shop.Label, shop.Duration, shop.Previous, shop.Delta, shop.Share));
    }

    [Fact]
    public void Top_activities_by_duration_skip_entries_without_activity()
    {
        TimeEntry[] entries =
        [
            E(Web, "19.10 09:00", "19.10 10:00", "ZW-1"),
            E(Web, "19.10 10:00", "19.10 13:00", "ZW-2"),
            E(Web, "20.10 09:00", "20.10 10:30", "ZW-1"),
            E(Shop, "20.10 11:00", "20.10 12:00", "ZW-1"),
            E(App, "20.10 13:00", "20.10 18:00"),
            E(Web, "12.10 09:00", "12.10 10:00", "ZW-2"),
        ];

        var report = ReportBuilder.Build(entries, Week, Berlin, topActivities: 2);

        Assert.Equal([("ZW-2", "Acme / Web", H(3), H(1)), ("ZW-1", "Acme / Web", H(2.5), TimeSpan.Zero)],
            report.Activities.Select(r => (r.Label, r.Detail!, r.Duration, r.Previous)));
    }

    [Fact]
    public void Empty_period_has_zero_shares()
    {
        var report = ReportBuilder.Build([E(Web, "12.10 09:00", "12.10 10:00")], Week, Berlin);

        Assert.Equal(TimeSpan.Zero, report.Total);
        Assert.All(report.Projects, r => Assert.Equal(0, r.Share));
        Assert.Empty(report.Activities);
    }

    [Fact]
    public void Input_entries_are_not_modified()
    {
        var entry = E(Web, "25.10 22:00", "26.10 02:00");

        ReportBuilder.Build([entry], Week, Berlin);

        Assert.Equal(L("26.10 02:00"), entry.End);
    }
}

public class ReportPeriodTests
{
    [Theory]
    [InlineData("2026-10-21", "2026-10-19")] // Mittwoch
    [InlineData("2026-10-19", "2026-10-19")] // Montag
    [InlineData("2026-10-25", "2026-10-19")] // Sonntag
    public void This_week_starts_on_monday(string today, string monday)
    {
        var week = ReportPeriod.ThisWeek(DateOnly.Parse(today));

        Assert.Equal(DateOnly.Parse(monday), week.From);
        Assert.Equal(7, week.Days);
    }

    [Fact]
    public void Month_presets()
    {
        var today = new DateOnly(2026, 1, 15);

        Assert.Equal(new ReportPeriod(new(2026, 1, 1), new(2026, 2, 1)), ReportPeriod.Month(today));
        Assert.Equal(new ReportPeriod(new(2025, 12, 1), new(2026, 1, 1)), ReportPeriod.Month(today, -1));
        Assert.Equal(new ReportPeriod(new(2026, 1, 1), new(2027, 1, 1)), ReportPeriod.ThisYear(today));
    }

    [Fact]
    public void Previous_of_whole_months_uses_calendar_months()
    {
        Assert.Equal(new ReportPeriod(new(2026, 2, 1), new(2026, 3, 1)), new ReportPeriod(new(2026, 3, 1), new(2026, 4, 1)).Previous());
        Assert.Equal(new ReportPeriod(new(2025, 1, 1), new(2026, 1, 1)), ReportPeriod.ThisYear(new(2026, 5, 5)).Previous());
    }

    [Fact]
    public void Previous_of_free_period_has_same_number_of_days()
    {
        var period = new ReportPeriod(new(2026, 10, 5), new(2026, 10, 15));

        Assert.Equal(new ReportPeriod(new(2026, 9, 25), new(2026, 10, 5)), period.Previous());
    }
}
