using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Data;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

/// <summary>Eine Projekt/Tätigkeit-Kombination für die Schnellwahl.</summary>
public sealed record RecentWork(Project Project, string? Activity);

/// <summary>Lesen und manuelles Bearbeiten von Zeiteinträgen.</summary>
public sealed class EntryService(IDbContextFactory<ZeitwerkDbContext> dbFactory, Clock clock, ChangeNotifier notifier)
{
    /// <summary>Alle Einträge, die den Zeitraum [<paramref name="from"/>, <paramref name="toExclusive"/>) berühren.</summary>
    public async Task<List<TimeEntry>> GetEntriesAsync(DateOnly from, DateOnly toExclusive)
    {
        var start = clock.StartOfDay(from);
        var end = clock.StartOfDay(toExclusive);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.TimeEntries.AsNoTracking()
            .Include(e => e.Project).ThenInclude(p => p.Customer)
            .Where(e => e.Start < end && (e.End == null || e.End > start))
            .OrderBy(e => e.Start)
            .ToListAsync();
    }

    /// <summary>Gebuchte Zeit je lokalem Tag im Zeitraum; offene Einträge zählen bis jetzt.</summary>
    public async Task<Dictionary<DateOnly, TimeSpan>> GetDayTotalsAsync(DateOnly from, DateOnly toExclusive)
    {
        var entries = await GetEntriesAsync(from, toExclusive);
        var now = clock.UtcNow;
        var totals = new Dictionary<DateOnly, TimeSpan>();
        for (var day = from; day < toExclusive; day = day.AddDays(1))
        {
            var dayStart = clock.StartOfDay(day);
            var dayEnd = clock.StartOfDay(day.AddDays(1));
            var sum = entries.Aggregate(TimeSpan.Zero, (acc, e) => acc + OverlapWith(e, dayStart, dayEnd, now));
            if (sum > TimeSpan.Zero)
                totals[day] = sum;
        }
        return totals;
    }

    public static TimeSpan OverlapWith(TimeEntry entry, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        var start = entry.Start > from ? entry.Start : from;
        var entryEnd = entry.End ?? (now > entry.Start ? now : entry.Start);
        var end = entryEnd < to ? entryEnd : to;
        return end > start ? end - start : TimeSpan.Zero;
    }

    /// <summary>Legt einen Eintrag an oder ändert ihn. Gibt eine Fehlermeldung zurück oder <c>null</c>.</summary>
    public async Task<string?> SaveAsync(int id, int projectId, string? activity, DateTimeOffset start, DateTimeOffset? end)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = id == 0 ? new TimeEntry() : await db.TimeEntries.FindAsync(id);
        if (entry is null)
            return "Der Eintrag existiert nicht mehr.";
        if (end is null && !entry.IsOpen)
            return "Bitte ein Ende angeben.";

        var others = await db.TimeEntries.AsNoTracking().Where(e => e.Id != id).ToListAsync();
        if (EntryRules.Validate(start, end, others) is { } error)
            return error;

        entry.ProjectId = projectId;
        entry.Activity = TimeEntry.NormalizeActivity(activity);
        entry.Start = start;
        entry.End = end;
        if (id == 0)
            db.TimeEntries.Add(entry);
        db.TimeEntries.AddRange(DaySplitter.Split(entry, clock.Zone));
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
        return null;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.TimeEntries.Where(e => e.Id == id).ExecuteDeleteAsync();
        notifier.NotifyChanged();
    }

    /// <summary>Zuletzt genutzte Projekt/Tätigkeit-Kombinationen aus nicht archivierten Projekten.</summary>
    public async Task<List<RecentWork>> GetRecentWorkAsync(int count = 6)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var recent = await db.TimeEntries.AsNoTracking()
            .Include(e => e.Project).ThenInclude(p => p.Customer)
            .Where(e => !e.Project.IsArchived && !e.Project.Customer.IsArchived)
            .OrderByDescending(e => e.Start)
            .Take(200)
            .ToListAsync();
        return recent
            .DistinctBy(e => (e.ProjectId, e.Activity))
            .Take(count)
            .Select(e => new RecentWork(e.Project, e.Activity))
            .ToList();
    }

    /// <summary>Frühere Tätigkeiten eines Projekts, zuletzt genutzte zuerst.</summary>
    public async Task<List<string>> GetActivitySuggestionsAsync(int projectId, string? search)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.TimeEntries.AsNoTracking().Where(e => e.ProjectId == projectId && e.Activity != null);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => EF.Functions.ILike(e.Activity!, $"%{search.Trim()}%"));
        return await query
            .GroupBy(e => e.Activity!)
            .OrderByDescending(g => g.Max(e => e.Start))
            .Select(g => g.Key)
            .Take(20)
            .ToListAsync();
    }
}
