namespace Zeitwerk.Web.Domain;

public sealed record TimerSettings(TimeSpan Tolerance, TimeZoneInfo TimeZone)
{
    public static readonly TimeSpan LongRunningThreshold = TimeSpan.FromHours(10);
}

/// <summary>Der Ausschnitt der Daten, den der Timer braucht.</summary>
/// <param name="Open">Der offene Zeiteintrag (laufend oder on hold), falls vorhanden.</param>
/// <param name="Last">Der abgeschlossene Zeiteintrag mit dem spätesten Ende, falls vorhanden.</param>
public sealed record TimerState(TimeEntry? Open, TimeEntry? Last);

/// <summary>Ergebnis einer Timer-Aktion. Geänderte Einträge werden direkt am Objekt angepasst;
/// hier stehen nur neue und zu löschende Einträge.</summary>
public sealed class TimerChanges
{
    public List<TimeEntry> Added { get; } = [];
    public List<TimeEntry> Removed { get; } = [];
}

/// <summary>Setzt die Timer-Regeln aus SPEC.md (R1–R14) um. Reine Logik ohne Datenbank.</summary>
public sealed class TimerEngine(TimerSettings settings)
{
    /// <summary>Start aus dem Leerlauf oder Wechsel des offenen Eintrags.</summary>
    public TimerChanges SwitchTo(TimerState state, int projectId, string? activity, DateTimeOffset now)
    {
        activity = TimeEntry.NormalizeActivity(activity);
        var changes = new TimerChanges();

        if (state.Open is not { } open)
        {
            StartFromIdle(state.Last, projectId, activity, now, changes);
            return changes;
        }

        if (open.IsSameWork(projectId, activity))
            return changes; // R9

        var end = TimeGrid.Ceil(now, settings.Tolerance);
        if (end <= open.Start)
        {
            // R6/R8: Der offene Eintrag hat noch keine Zeit verbraucht.
            ReplaceUnstarted(open, state.Last, projectId, activity, changes);
            return changes;
        }

        open.End = end; // R4/R5
        var next = new TimeEntry { ProjectId = projectId, Activity = activity, Start = end };
        changes.Added.Add(next);
        changes.Added.AddRange(DaySplitter.Split(open, settings.TimeZone));
        return changes;
    }

    public TimerChanges Stop(TimerState state, DateTimeOffset now)
    {
        var changes = new TimerChanges();
        if (state.Open is not { } open)
            return changes;

        var end = TimeGrid.Ceil(now, settings.Tolerance);
        if (end <= open.Start)
        {
            changes.Removed.Add(open); // R11/R12
            return changes;
        }

        open.End = end; // R10
        changes.Added.AddRange(DaySplitter.Split(open, settings.TimeZone));
        return changes;
    }

    private static void StartFromIdle(TimeEntry? last, int projectId, string? activity, DateTimeOffset now, TimerChanges changes)
    {
        var start = TimeGrid.Floor(now); // R1
        if (last?.End > start)
            start = last.End.Value; // R2

        if (last is not null && last.End == start && last.IsSameWork(projectId, activity))
        {
            last.End = null; // R3
            return;
        }

        changes.Added.Add(new TimeEntry { ProjectId = projectId, Activity = activity, Start = start });
    }

    private static void ReplaceUnstarted(TimeEntry open, TimeEntry? last, int projectId, string? activity, TimerChanges changes)
    {
        if (last is not null && last.End == open.Start && last.IsSameWork(projectId, activity))
        {
            // R7: Zurück zum vorherigen Eintrag.
            changes.Removed.Add(open);
            last.End = null;
            return;
        }

        open.ProjectId = projectId;
        open.Activity = activity;
    }
}
