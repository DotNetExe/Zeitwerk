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

    /// <summary>Teilt einen abgeschlossenen Eintrag bei <paramref name="at"/>; der zweite Teil bekommt die angegebene Arbeit.</summary>
    public async Task<string?> SplitAsync(int id, DateTimeOffset at, int projectId, string? activity)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.TimeEntries.FindAsync(id);
        if (entry is null)
            return "Der Eintrag existiert nicht mehr.";
        if (SplitMergeRules.ValidateSplit(entry, at) is { } error)
            return error;

        db.TimeEntries.Add(SplitMergeRules.Split(entry, at, projectId, activity));
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
        return null;
    }

    /// <summary>Abgeschlossene Einträge, die direkt vor oder nach dem Eintrag liegen und mit ihm zusammengeführt werden können.</summary>
    public async Task<List<TimeEntry>> GetMergePartnersAsync(TimeEntry entry)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var neighbors = await db.TimeEntries.AsNoTracking()
            .Include(e => e.Project).ThenInclude(p => p.Customer)
            .Where(e => e.Id != entry.Id && (e.End == entry.Start || (entry.End != null && e.Start == entry.End)))
            .OrderBy(e => e.Start)
            .ToListAsync();
        return neighbors
            .Where(n => SplitMergeRules.CanMerge(n, entry, clock.Zone) || SplitMergeRules.CanMerge(entry, n, clock.Zone))
            .ToList();
    }

    /// <summary>Führt zwei aneinander grenzende Einträge zusammen; die Reihenfolge der Ids ist egal.</summary>
    public async Task<string?> MergeAsync(int id, int otherId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var pair = await db.TimeEntries.Where(e => e.Id == id || e.Id == otherId).OrderBy(e => e.Start).ToListAsync();
        if (pair.Count != 2)
            return "Der Eintrag existiert nicht mehr.";
        if (!SplitMergeRules.CanMerge(pair[0], pair[1], clock.Zone))
            return "Die Einträge können nicht zusammengeführt werden.";

        SplitMergeRules.Merge(pair[0], pair[1], clock.Zone);
        db.TimeEntries.Remove(pair[1]);
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
        return null;
    }

    /// <summary>Führt alle zusammenführbaren Einträge des Tages zusammen.</summary>
    public async Task MergeAllAsync(DateOnly date)
    {
        var start = clock.StartOfDay(date);
        var end = clock.StartOfDay(date.AddDays(1));
        await using var db = await dbFactory.CreateDbContextAsync();
        var entries = await db.TimeEntries.Where(e => e.Start >= start && e.Start < end).ToListAsync();
        var removed = SplitMergeRules.MergeAll(entries, clock.Zone);
        if (removed.Count == 0)
            return;
        db.TimeEntries.RemoveRange(removed);
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
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
