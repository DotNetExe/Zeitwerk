using Microsoft.EntityFrameworkCore;
using Zeitwerk.Web.Data;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Services;

/// <summary>Soll-Arbeitszeit, Abwesenheiten und Kontostart des Überstundenkontos.</summary>
public sealed class OvertimeService(IDbContextFactory<ZeitwerkDbContext> dbFactory, Clock clock, ChangeNotifier notifier)
{
    public async Task<OvertimeAccount> GetAccountAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var start = await LoadStartAsync(db);
        var schedules = await db.WorkSchedules.AsNoTracking().ToListAsync();
        var absences = await db.Absences.AsNoTracking().ToDictionaryAsync(a => a.Date, a => a.Kind);
        return new OvertimeAccount(start, schedules, absences);
    }

    /// <summary>Gespeicherter Kontostart; ohne Einstellung der Tag des ersten Zeiteintrags (sonst heute) mit Saldo 0.</summary>
    public async Task<AccountStart> GetStartAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await LoadStartAsync(db);
    }

    private async Task<AccountStart> LoadStartAsync(ZeitwerkDbContext db)
    {
        if (await db.AccountStarts.AsNoTracking().FirstOrDefaultAsync() is { } start)
            return start;
        var first = await db.TimeEntries.MinAsync(e => (DateTimeOffset?)e.Start);
        return new AccountStart { Date = first is { } f ? clock.LocalDate(f) : clock.Today };
    }

    public async Task SaveStartAsync(DateOnly date, TimeSpan balance)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var start = await db.AccountStarts.FirstOrDefaultAsync() ?? db.AccountStarts.Add(new AccountStart()).Entity;
        start.Date = date;
        start.Balance = balance;
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
    }

    /// <summary>Alle Versionen, neueste zuerst; ohne gespeicherte Version der Standard.</summary>
    public async Task<List<WorkSchedule>> GetSchedulesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WorkSchedules.AsNoTracking().OrderByDescending(s => s.ValidFrom).ToListAsync();
    }

    /// <summary>Legt eine Version an oder ersetzt die mit demselben Gültig-ab.</summary>
    public async Task SaveScheduleAsync(WorkSchedule schedule)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.WorkSchedules.FirstOrDefaultAsync(s => s.ValidFrom == schedule.ValidFrom)
                       ?? db.WorkSchedules.Add(new WorkSchedule { ValidFrom = schedule.ValidFrom }).Entity;
        foreach (var day in Enum.GetValues<DayOfWeek>())
            existing[day] = schedule[day];
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
    }

    public async Task DeleteScheduleAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.WorkSchedules.Where(s => s.Id == id).ExecuteDeleteAsync();
        notifier.NotifyChanged();
    }

    /// <summary>Setzt die Abwesenheit eines Tages; <c>null</c> entfernt sie.</summary>
    public async Task SetAbsenceAsync(DateOnly date, AbsenceKind? kind)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var absence = await db.Absences.FirstOrDefaultAsync(a => a.Date == date);
        if (kind is null)
        {
            if (absence is not null)
                db.Absences.Remove(absence);
        }
        else
        {
            absence ??= db.Absences.Add(new Absence { Date = date }).Entity;
            absence.Kind = kind.Value;
        }
        await db.SaveChangesAsync();
        notifier.NotifyChanged();
    }
}
