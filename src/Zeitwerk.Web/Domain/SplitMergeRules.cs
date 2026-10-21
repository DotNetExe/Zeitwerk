namespace Zeitwerk.Web.Domain;

/// <summary>Teilen eines Zeiteintrags und Zusammenführen direkt aneinander grenzender Zeiteinträge.</summary>
public static class SplitMergeRules
{
    /// <summary>Rasterzeitpunkte strikt zwischen Beginn und Ende; leer bei offenen oder zu kurzen Einträgen.</summary>
    public static List<DateTimeOffset> SplitPoints(TimeEntry entry)
    {
        var points = new List<DateTimeOffset>();
        if (entry.End is not { } end)
            return points;
        for (var t = entry.Start + TimeGrid.Slot; t < end; t += TimeGrid.Slot)
            points.Add(t);
        return points;
    }

    /// <returns>Fehlermeldung oder <c>null</c>, wenn <paramref name="entry"/> bei <paramref name="at"/> geteilt werden kann.</returns>
    public static string? ValidateSplit(TimeEntry entry, DateTimeOffset at)
    {
        if (entry.End is not { } end)
            return "Ein offener Eintrag kann nicht geteilt werden.";
        if (!TimeGrid.IsOnGrid(at))
            return "Der Teilungszeitpunkt muss im 5-Minuten-Raster liegen.";
        if (at <= entry.Start || at >= end)
            return "Der Teilungszeitpunkt muss zwischen Beginn und Ende liegen.";
        return null;
    }

    /// <summary>Kürzt <paramref name="entry"/> auf [Beginn, <paramref name="at"/>) und liefert den zweiten Teil.</summary>
    public static TimeEntry Split(TimeEntry entry, DateTimeOffset at, int projectId, string? activity)
    {
        if (ValidateSplit(entry, at) is { } error)
            throw new ArgumentException(error, nameof(at));

        var second = new TimeEntry
        {
            ProjectId = projectId,
            Activity = TimeEntry.NormalizeActivity(activity),
            Start = at,
            End = entry.End,
        };
        entry.End = at;
        return second;
    }

    /// <summary>
    /// <paramref name="second"/> schließt direkt an <paramref name="first"/> an, beide sind abgeschlossen, haben gleiches
    /// Projekt und gleiche Tätigkeit, und der zusammengeführte Eintrag bleibt innerhalb eines lokalen Tages (R14).
    /// </summary>
    public static bool CanMerge(TimeEntry first, TimeEntry second, TimeZoneInfo zone) =>
        first.End is { } firstEnd
        && second.End is { } secondEnd
        && firstEnd == second.Start
        && first.IsSameWork(second.ProjectId, second.Activity)
        && secondEnd <= DaySplitter.NextMidnight(first.Start, zone);

    /// <summary>Verlängert <paramref name="first"/> bis zum Ende von <paramref name="second"/>; der Aufrufer entfernt <paramref name="second"/>.</summary>
    public static void Merge(TimeEntry first, TimeEntry second, TimeZoneInfo zone)
    {
        if (!CanMerge(first, second, zone))
            throw new ArgumentException("Die Einträge können nicht zusammengeführt werden.", nameof(second));
        first.End = second.End;
    }

    /// <summary>Alle Paare (vorher, nachher), die zusammengeführt werden können, nach Beginn sortiert.</summary>
    public static List<(TimeEntry First, TimeEntry Second)> FindMergeablePairs(IEnumerable<TimeEntry> entries, TimeZoneInfo zone)
    {
        var sorted = entries.OrderBy(e => e.Start).ToList();
        return sorted.Zip(sorted.Skip(1))
            .Where(p => CanMerge(p.First, p.Second, zone))
            .ToList();
    }

    /// <summary>Führt alle Ketten zusammenführbarer Einträge zu je einem zusammen und liefert die überflüssigen Einträge.</summary>
    public static List<TimeEntry> MergeAll(IEnumerable<TimeEntry> entries, TimeZoneInfo zone)
    {
        var removed = new List<TimeEntry>();
        TimeEntry? current = null;
        foreach (var entry in entries.OrderBy(e => e.Start))
        {
            if (current is not null && CanMerge(current, entry, zone))
            {
                current.End = entry.End;
                removed.Add(entry);
            }
            else
            {
                current = entry;
            }
        }
        return removed;
    }
}
