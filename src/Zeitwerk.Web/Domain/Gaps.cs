namespace Zeitwerk.Web.Domain;

/// <summary>Unbelegte Zeit zwischen zwei Zeiteinträgen eines Tages. Nur ein Hinweis – eine Lücke kann eine Pause sein.</summary>
public sealed record Gap(DateTimeOffset Start, DateTimeOffset End, TimeEntry Before, TimeEntry After)
{
    public TimeSpan Duration => End - Start;
}

/// <summary>Findet Lücken zwischen dem ersten und dem letzten Zeiteintrag eines Tages.</summary>
public static class Gaps
{
    /// <summary>
    /// Lücken innerhalb von [<paramref name="dayStart"/>, <paramref name="dayEnd"/>), nach Beginn sortiert.
    /// Vor dem ersten und nach dem letzten Eintrag gibt es keine Lücke; ein offener Eintrag (laufend oder
    /// on hold) belegt ab seinem Beginn alles.
    /// </summary>
    public static List<Gap> Find(IEnumerable<TimeEntry> entries, DateTimeOffset dayStart, DateTimeOffset dayEnd)
    {
        var gaps = new List<Gap>();
        TimeEntry? before = null;
        DateTimeOffset coveredUntil = default;

        foreach (var entry in entries.Where(e => e.Start < dayEnd && (e.End is null || e.End > dayStart)).OrderBy(e => e.Start))
        {
            var start = entry.Start > dayStart ? entry.Start : dayStart;
            if (before is not null && start > coveredUntil)
                gaps.Add(new Gap(coveredUntil, start, before, entry));

            var end = entry.End is { } e && e < dayEnd ? e : dayEnd;
            if (before is null || end > coveredUntil)
            {
                before = entry;
                coveredUntil = end;
            }
            if (entry.IsOpen)
                break;
        }
        return gaps;
    }
}
