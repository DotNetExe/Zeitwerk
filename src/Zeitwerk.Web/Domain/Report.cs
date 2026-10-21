namespace Zeitwerk.Web.Domain;

/// <summary>Ein Auswertungszeitraum aus lokalen Tagen [<see cref="From"/>, <see cref="ToExclusive"/>).</summary>
public sealed record ReportPeriod(DateOnly From, DateOnly ToExclusive)
{
    public DateOnly To => ToExclusive.AddDays(-1);

    public int Days => ToExclusive.DayNumber - From.DayNumber;

    /// <summary>Der gleich lange Zeitraum direkt davor. Umfasst der Zeitraum ganze Monate,
    /// ist der Vorzeitraum ebenso viele ganze Monate (März → Februar, nicht 31 Tage).</summary>
    public ReportPeriod Previous()
    {
        if (From.Day == 1 && ToExclusive.Day == 1)
        {
            var months = (ToExclusive.Year - From.Year) * 12 + ToExclusive.Month - From.Month;
            return new(From.AddMonths(-months), From);
        }
        return new(From.AddDays(-Days), From);
    }

    /// <summary>Montag bis Sonntag der Woche von <paramref name="today"/>.</summary>
    public static ReportPeriod ThisWeek(DateOnly today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return new(monday, monday.AddDays(7));
    }

    public static ReportPeriod Month(DateOnly today, int offset = 0)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(offset);
        return new(first, first.AddMonths(1));
    }

    public static ReportPeriod ThisYear(DateOnly today) => new(new(today.Year, 1, 1), new(today.Year + 1, 1, 1));
}

/// <summary>Eine Zeile der Auswertung: gebuchte Zeit im Zeitraum und im Vorzeitraum.</summary>
/// <param name="Color">Projektfarbe; <c>null</c> bei Kunden.</param>
/// <param name="Detail">Zusatz, z. B. Kunde des Projekts oder Projekt der Tätigkeit.</param>
public sealed record ReportRow(string Label, string? Detail, string? Color, TimeSpan Duration, TimeSpan Previous, double Share)
{
    public TimeSpan Delta => Duration - Previous;
}

public sealed record Report(
    ReportPeriod Period,
    ReportPeriod PreviousPeriod,
    TimeSpan Total,
    TimeSpan PreviousTotal,
    IReadOnlyList<ReportRow> Customers,
    IReadOnlyList<ReportRow> Projects,
    IReadOnlyList<ReportRow> Activities)
{
    public TimeSpan Delta => Total - PreviousTotal;
}

/// <summary>Aggregiert Zeiteinträge für die Auswertung. Reine Logik ohne Datenbank.</summary>
public static class ReportBuilder
{
    /// <summary>Baut die Auswertung für <paramref name="period"/> samt Vergleich mit dem Vorzeitraum.</summary>
    /// <param name="entries">Einträge beider Zeiträume; Projekt und Kunde müssen geladen sein. Offene Einträge
    /// zählen nicht. Einträge über Mitternacht werden wie im Kalender per <see cref="DaySplitter"/> auf Tage verteilt.</param>
    public static Report Build(IEnumerable<TimeEntry> entries, ReportPeriod period, TimeZoneInfo zone, int topActivities = 10)
    {
        var previous = period.Previous();
        var current = Pieces(entries, period, zone);
        var before = Pieces(entries, previous, zone);
        var total = Sum(current);
        var previousTotal = Sum(before);

        // Zeilen aus beiden Zeiträumen, damit auch weggefallene Kunden/Projekte im Vergleich auftauchen.
        List<ReportRow> Rows<TKey>(Func<TimeEntry, TKey> key, Func<TimeEntry, string> label,
            Func<TimeEntry, string?> detail, Func<TimeEntry, string?> color) where TKey : notnull
        {
            var now = current.GroupBy(key).ToDictionary(g => g.Key, Sum);
            var prev = before.GroupBy(key).ToDictionary(g => g.Key, Sum);
            return current.Concat(before)
                .DistinctBy(key)
                .Select(sample =>
                {
                    var duration = now.GetValueOrDefault(key(sample));
                    return new ReportRow(label(sample), detail(sample), color(sample), duration,
                        prev.GetValueOrDefault(key(sample)), total > TimeSpan.Zero ? duration / total : 0);
                })
                .OrderByDescending(r => r.Duration)
                .ThenByDescending(r => r.Previous)
                .ThenBy(r => r.Label, StringComparer.CurrentCulture)
                .ToList();
        }

        var customers = Rows(e => e.Project.CustomerId, e => e.Project.Customer.Name, _ => null, _ => null);
        var projects = Rows(e => e.ProjectId, e => e.Project.Name, e => e.Project.Customer.Name, e => e.Project.Color);

        // Einträge ohne Tätigkeit tauchen in der Top-Liste nicht auf.
        current = current.Where(e => e.Activity is not null).ToList();
        before = before.Where(e => e.Activity is not null).ToList();
        var activities = Rows(e => (e.ProjectId, e.Activity), e => e.Activity!, e => e.Project.DisplayName, e => e.Project.Color)
            .Where(r => r.Duration > TimeSpan.Zero)
            .Take(topActivities)
            .ToList();

        return new Report(period, previous, total, previousTotal, customers, projects, activities);
    }

    /// <summary>Die Tagesstücke abgeschlossener Einträge, deren lokaler Tag im Zeitraum liegt.</summary>
    public static List<TimeEntry> Pieces(IEnumerable<TimeEntry> entries, ReportPeriod period, TimeZoneInfo zone)
    {
        var from = DaySplitter.StartOfDay(period.From, zone);
        var to = DaySplitter.StartOfDay(period.ToExclusive, zone);
        var pieces = new List<TimeEntry>();
        foreach (var entry in entries.Where(e => e.End is not null && e.Start < to && e.End > from))
        {
            var copy = new TimeEntry
            {
                Id = entry.Id, ProjectId = entry.ProjectId, Project = entry.Project,
                Activity = entry.Activity, Start = entry.Start, End = entry.End,
            };
            var split = DaySplitter.Split(copy, zone);
            foreach (var piece in split)
                piece.Project = entry.Project;
            pieces.AddRange(new[] { copy }.Concat(split).Where(p => p.Start >= from && p.Start < to));
        }
        return pieces;
    }

    private static TimeSpan Sum(IEnumerable<TimeEntry> pieces) =>
        pieces.Aggregate(TimeSpan.Zero, (acc, p) => acc + (p.End!.Value - p.Start));
}
