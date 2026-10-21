namespace Zeitwerk.Web.Domain;

/// <summary>Soll, Ist und Saldo eines Zeitraums; nur Tage, die zum Konto zählen.</summary>
public sealed record PeriodBalance(int Days, TimeSpan Target, TimeSpan Actual)
{
    public TimeSpan Delta => Actual - Target;
}

/// <summary>
/// Überstundenkonto: Ein Tag zählt, wenn er zwischen Kontostart und heute (einschließlich) liegt.
/// Saldo = Startsaldo + Σ (Ist − Soll) aller zählenden Tage.
/// </summary>
public sealed class OvertimeAccount
{
    private readonly List<WorkSchedule> _schedules;
    private readonly IReadOnlyDictionary<DateOnly, AbsenceKind> _absences;

    public OvertimeAccount(AccountStart start, IEnumerable<WorkSchedule> schedules, IReadOnlyDictionary<DateOnly, AbsenceKind> absences)
    {
        Start = start.Date;
        StartBalance = start.Balance;
        _schedules = schedules.OrderBy(s => s.ValidFrom).ToList();
        _absences = absences;
    }

    public DateOnly Start { get; }
    public TimeSpan StartBalance { get; }

    /// <summary>Die Version mit dem spätesten Gültig-ab ≤ <paramref name="day"/>; vor der ersten Version gilt der Standard.</summary>
    public WorkSchedule ScheduleFor(DateOnly day) => _schedules.LastOrDefault(s => s.ValidFrom <= day) ?? WorkSchedule.Default;

    public AbsenceKind? AbsenceOn(DateOnly day) => _absences.TryGetValue(day, out var kind) ? kind : null;

    /// <summary>Soll eines Tages laut Plan; an Abwesenheitstagen 0.</summary>
    public TimeSpan TargetFor(DateOnly day) => AbsenceOn(day) is null ? ScheduleFor(day)[day.DayOfWeek] : TimeSpan.Zero;

    public bool Counts(DateOnly day, DateOnly today) => day >= Start && day <= today;

    /// <summary>Soll/Ist/Saldo der zählenden Tage in [<paramref name="from"/>, <paramref name="toExclusive"/>).</summary>
    public PeriodBalance Period(DateOnly from, DateOnly toExclusive, DateOnly today, IReadOnlyDictionary<DateOnly, TimeSpan> actual)
    {
        var days = 0;
        TimeSpan target = TimeSpan.Zero, worked = TimeSpan.Zero;
        for (var day = from > Start ? from : Start; day < toExclusive && day <= today; day = day.AddDays(1))
        {
            days++;
            target += TargetFor(day);
            worked += actual.GetValueOrDefault(day);
        }
        return new PeriodBalance(days, target, worked);
    }

    /// <summary>Gesamtsaldo am Ende von <paramref name="today"/>.</summary>
    public TimeSpan Balance(DateOnly today, IReadOnlyDictionary<DateOnly, TimeSpan> actual) =>
        StartBalance + Period(Start, today.AddDays(1), today, actual).Delta;
}
