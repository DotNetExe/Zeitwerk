using Zeitwerk.Web.Domain;

namespace Zeitwerk.Tests;

/// <summary>In-Memory-Ersatz für die Datenbank: leitet den TimerState ab und wendet TimerChanges an.</summary>
internal sealed class TimerHarness(TimeZoneInfo? zone = null, int toleranceSeconds = 60)
{
    public const int A = 1, B = 2, C = 3;

    private readonly TimerEngine _engine = new(new TimerSettings(
        TimeSpan.FromSeconds(toleranceSeconds), zone ?? TimeZoneInfo.Utc));

    public List<TimeEntry> Entries { get; } = [];

    public void SwitchTo(int project, string at, string? activity = null) =>
        Apply(_engine.SwitchTo(State, project, activity, T(at)));

    public void Stop(string at) => Apply(_engine.Stop(State, T(at)));

    public TimeEntry? Open => Entries.SingleOrDefault(e => e.IsOpen);

    /// <summary>Alle Einträge als "Projekt Beginn-Ende[ Tätigkeit]", nach Beginn sortiert.</summary>
    public string[] Timeline => Entries
        .OrderBy(e => e.Start)
        .Select(e => $"{e.ProjectId} {Fmt(e.Start)}-{(e.End is { } end ? Fmt(end) : "…")}{(e.Activity is null ? "" : " " + e.Activity)}")
        .ToArray();

    private TimerState State => new(
        Open,
        Entries.Where(e => !e.IsOpen).MaxBy(e => e.End));

    private void Apply(TimerChanges changes)
    {
        foreach (var removed in changes.Removed)
            Entries.Remove(removed);
        Entries.AddRange(changes.Added);
    }

    /// <summary>"09:03", "09:03:20" (am 21.10.2026) oder "22.10 01:00" für einen anderen Oktobertag, jeweils UTC.</summary>
    public static DateTimeOffset T(string at)
    {
        var day = 21;
        if (at.Contains(' '))
        {
            var parts = at.Split(' ');
            day = int.Parse(parts[0].Split('.')[0]);
            at = parts[1];
        }
        var time = TimeOnly.Parse(at);
        return new DateTimeOffset(2026, 10, day, time.Hour, time.Minute, time.Second, TimeSpan.Zero);
    }

    private static string Fmt(DateTimeOffset t) =>
        t.Day == 21 ? t.ToString("HH:mm") : t.ToString("dd.HH:mm");
}
