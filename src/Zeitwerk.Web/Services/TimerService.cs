using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Data;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

/// <summary>Benachrichtigt alle offenen Ansichten (auch in anderen Tabs), wenn sich Zeiteinträge geändert haben.</summary>
public sealed class ChangeNotifier(ILogger<ChangeNotifier> logger)
{
    public event Action? Changed;

    /// <summary>Ruft jede Ansicht einzeln auf, damit eine fehlerhafte (z. B. aus einer getrennten Verbindung) die übrigen nicht blockiert.</summary>
    public void NotifyChanged()
    {
        foreach (var handler in Changed?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ansicht {View} konnte nicht benachrichtigt werden", handler.Target?.GetType().Name);
            }
        }
    }
}

/// <summary>Führt Timer-Aktionen auf der Datenbank aus.</summary>
public sealed class TimerService(IDbContextFactory<ZeitwerkDbContext> dbFactory, Clock clock, ChangeNotifier notifier)
{
    // Einzelplatz: Ein globaler Lock reicht, damit zwei Tabs nicht gleichzeitig umschalten.
    private static readonly SemaphoreSlim Lock = new(1, 1);

    /// <summary>Der offene Zeiteintrag inklusive Projekt und Kunde, falls vorhanden.</summary>
    public async Task<TimeEntry?> GetOpenAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.TimeEntries.AsNoTracking()
            .Include(e => e.Project).ThenInclude(p => p.Customer)
            .SingleOrDefaultAsync(e => e.End == null);
    }

    public Task SwitchToAsync(int projectId, string? activity) =>
        RunAsync((engine, state, now) => engine.SwitchTo(state, projectId, activity, now));

    public Task StopAsync() =>
        RunAsync((engine, state, now) => engine.Stop(state, now));

    /// <summary>R13: Tätigkeit des offenen Eintrags ändern, ohne zu wechseln.</summary>
    public async Task UpdateOpenActivityAsync(string? activity)
    {
        await Lock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var open = await db.TimeEntries.SingleOrDefaultAsync(e => e.End == null);
            if (open is null)
                return;
            open.Activity = TimeEntry.NormalizeActivity(activity);
            await db.SaveChangesAsync();
        }
        finally
        {
            Lock.Release();
        }
        notifier.NotifyChanged();
    }

    private async Task RunAsync(Func<TimerEngine, TimerState, DateTimeOffset, TimerChanges> action)
    {
        await Lock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            // Ohne Tracking laden: Die Engine ändert die Objekte direkt, gespeichert wird schrittweise.
            var open = await db.TimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.End == null);
            var last = await db.TimeEntries.AsNoTracking().Where(e => e.End != null)
                .OrderByDescending(e => e.End).FirstOrDefaultAsync();

            var changes = action(new TimerEngine(clock.TimerSettings), new TimerState(open, last), clock.UtcNow);

            // Reihenfolge wegen des Unique-Index auf dem offenen Eintrag:
            // erst löschen, dann ändern (schließen/wieder öffnen), dann neue Einträge anlegen.
            db.TimeEntries.RemoveRange(changes.Removed);
            await db.SaveChangesAsync();
            db.TimeEntries.UpdateRange(new[] { open, last }.OfType<TimeEntry>().Except(changes.Removed));
            await db.SaveChangesAsync();
            db.TimeEntries.AddRange(changes.Added);
            await db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        finally
        {
            Lock.Release();
        }
        notifier.NotifyChanged();
    }
}
