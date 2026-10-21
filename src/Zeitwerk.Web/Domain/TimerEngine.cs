namespace Zeitwerk.Web.Domain;

public sealed record TimerSettings(TimeSpan Tolerance, TimeZoneInfo TimeZone)
{
    public static readonly TimeSpan LongRunningThreshold = TimeSpan.FromHours(10);

    /// <summary>Vorgeschlagenes Ende eines vergessenen Timers, wenn keine typische Dauer bekannt ist (R16).</summary>
    public static readonly TimeOnly DefaultForgottenEnd = new(18, 0);
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

/// <summary>Setzt die Timer-Regeln aus SPEC.md (R1–R16) um. Reine Logik ohne Datenbank.</summary>
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

    /// <summary>R15/R16: Der offene Eintrag läuft länger als 10 h oder über Mitternacht hinaus.</summary>
    public bool IsForgotten(TimeEntry open, DateTimeOffset now) =>
        open.IsOpen && !open.IsOnHold(now) &&
        (open.Duration(now) > TimerSettings.LongRunningThreshold || DaySplitter.NextMidnight(open.Start, settings.TimeZone) <= now);

    /// <summary>R16: Vorschlag für das Ende eines vergessenen Timers – Beginn + typische Dauer, sonst 18:00 am Tag
    /// des Beginns (beginnt der Eintrag später: eine Stunde nach Beginn); nie nach <c>abrunden(jetzt)</c>.</summary>
    public DateTimeOffset SuggestForgottenEnd(TimeEntry open, TimeSpan? typicalDuration, DateTimeOffset now)
    {
        DateTimeOffset end;
        if (typicalDuration > TimeSpan.Zero)
            end = TimeGrid.Ceil(open.Start + typicalDuration.Value, TimeSpan.Zero);
        else
        {
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(open.Start, settings.TimeZone).DateTime);
            var local = day.ToDateTime(TimerSettings.DefaultForgottenEnd, DateTimeKind.Unspecified);
            end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, settings.TimeZone), TimeSpan.Zero);
            if (end <= open.Start)
                end = open.Start + TimeSpan.FromHours(1);
        }
        var latest = TimeGrid.Floor(now);
        return end < latest ? end : latest;
    }

    /// <summary>R16: Prüft ein nachgetragenes Ende für den offenen Eintrag.</summary>
    /// <returns>Fehlermeldung oder <c>null</c>, wenn das Ende gültig ist.</returns>
    public static string? ValidateStopAt(TimerState state, DateTimeOffset end, DateTimeOffset now)
    {
        if (state.Open is not { } open)
            return "Es läuft kein Timer.";
        if (!TimeGrid.IsOnGrid(end))
            return "Das Ende muss im 5-Minuten-Raster liegen.";
        if (end <= open.Start)
            return "Das Ende muss nach dem Beginn liegen.";
        if (end > now)
            return "Das Ende darf nicht in der Zukunft liegen.";
        return null;
    }

    /// <summary>R16: Stoppt den offenen Eintrag mit einem nachgetragenen Ende (aufgeteilt nach R14) und startet auf
    /// Wunsch dieselbe Arbeit ab jetzt neu – wie ein Start aus dem Leerlauf (R1–R3).</summary>
    public TimerChanges StopAt(TimerState state, DateTimeOffset end, DateTimeOffset now, bool restart)
    {
        if (ValidateStopAt(state, end, now) is { } error)
            throw new ArgumentException(error, nameof(end));

        var open = state.Open!;
        var changes = new TimerChanges();
        open.End = end;
        changes.Added.AddRange(DaySplitter.Split(open, settings.TimeZone));

        if (restart)
            StartFromIdle(changes.Added.LastOrDefault() ?? open, open.ProjectId, open.Activity, now, changes);
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
