using Zeitwerk.Web.Domain;

namespace Zeitwerk.Tests;

public class OvertimeAccountTests
{
    // Mo 19.10.2026 … So 25.10.2026
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static TimeSpan H(double hours) => TimeSpan.FromHours(hours);

    private static OvertimeAccount Account(DateOnly? start = null, TimeSpan? startBalance = null,
        IEnumerable<WorkSchedule>? schedules = null, Dictionary<DateOnly, AbsenceKind>? absences = null) =>
        new(new AccountStart { Date = start ?? Monday, Balance = startBalance ?? TimeSpan.Zero },
            schedules ?? [], absences ?? []);

    private static Dictionary<DateOnly, TimeSpan> Worked(params double[] hoursFromMonday) =>
        hoursFromMonday.Select((h, i) => (Day: Monday.AddDays(i), Hours: H(h))).ToDictionary(x => x.Day, x => x.Hours);

    [Fact]
    public void Default_schedule_is_eight_hours_monday_to_friday()
    {
        var account = Account();
        Assert.Equal(H(8), account.TargetFor(Monday));
        Assert.Equal(H(8), account.TargetFor(Monday.AddDays(4)));
        Assert.Equal(TimeSpan.Zero, account.TargetFor(Monday.AddDays(5)));
        Assert.Equal(TimeSpan.Zero, account.TargetFor(Monday.AddDays(6)));
    }

    [Fact]
    public void Full_week_with_overtime()
    {
        var account = Account();
        var week = account.Period(Monday, Monday.AddDays(7), Monday.AddDays(6), Worked(9, 8, 8, 8, 7.5, 1));

        Assert.Equal(7, week.Days);
        Assert.Equal(H(40), week.Target);
        Assert.Equal(H(41.5), week.Actual);
        Assert.Equal(H(1.5), week.Delta);
    }

    [Fact]
    public void Days_after_today_do_not_count()
    {
        var account = Account();
        var week = account.Period(Monday, Monday.AddDays(7), Monday.AddDays(2), Worked(8, 8, 3));

        Assert.Equal(3, week.Days);
        Assert.Equal(H(24), week.Target);
        Assert.Equal(H(-5), week.Delta);
    }

    [Fact]
    public void Days_before_account_start_do_not_count()
    {
        var account = Account(start: Monday.AddDays(3));
        var week = account.Period(Monday, Monday.AddDays(7), Monday.AddDays(6), Worked(10, 10, 10, 8, 9));

        Assert.Equal(H(16), week.Target);
        Assert.Equal(H(17), week.Actual);
    }

    [Fact]
    public void Period_entirely_before_start_is_empty()
    {
        var account = Account(start: Monday.AddDays(7));
        var week = account.Period(Monday, Monday.AddDays(7), Monday.AddDays(10), Worked(8));

        Assert.Equal(0, week.Days);
        Assert.Equal(TimeSpan.Zero, week.Delta);
    }

    [Fact]
    public void Balance_adds_start_balance()
    {
        var account = Account(startBalance: H(-2.5));
        Assert.Equal(H(-1.5), account.Balance(Monday.AddDays(1), Worked(9, 8)));
    }

    [Fact]
    public void Balance_before_start_is_start_balance()
    {
        var account = Account(start: Monday.AddDays(3), startBalance: H(4));
        Assert.Equal(H(4), account.Balance(Monday, Worked(8)));
    }

    [Theory]
    [InlineData(AbsenceKind.Vacation)]
    [InlineData(AbsenceKind.Sick)]
    [InlineData(AbsenceKind.Holiday)]
    public void Absence_sets_target_to_zero(AbsenceKind kind)
    {
        var account = Account(absences: new() { [Monday.AddDays(1)] = kind });
        var week = account.Period(Monday, Monday.AddDays(7), Monday.AddDays(6), Worked(8, 0, 8, 8, 8));

        Assert.Equal(TimeSpan.Zero, account.TargetFor(Monday.AddDays(1)));
        Assert.Equal(H(32), week.Target);
        Assert.Equal(TimeSpan.Zero, week.Delta);
    }

    [Fact]
    public void Work_on_an_absence_day_is_overtime()
    {
        var account = Account(absences: new() { [Monday] = AbsenceKind.Holiday });
        Assert.Equal(H(2), account.Balance(Monday, Worked(2)));
    }

    [Fact]
    public void Schedule_change_does_not_affect_earlier_days()
    {
        var partTime = new WorkSchedule { ValidFrom = Monday.AddDays(3), Thursday = H(4), Friday = TimeSpan.Zero };
        var account = Account(schedules: [new WorkSchedule { ValidFrom = Monday }, partTime]);

        Assert.Equal(H(8), account.TargetFor(Monday.AddDays(2)));
        Assert.Equal(H(4), account.TargetFor(Monday.AddDays(3)));
        Assert.Equal(TimeSpan.Zero, account.TargetFor(Monday.AddDays(4)));
        Assert.Equal(H(8), account.TargetFor(Monday.AddDays(7)));
    }

    [Fact]
    public void Schedules_are_ordered_by_valid_from()
    {
        var later = new WorkSchedule { ValidFrom = Monday.AddDays(1), Tuesday = H(6) };
        var earlier = new WorkSchedule { ValidFrom = Monday.AddDays(-30), Tuesday = H(2) };
        var account = Account(schedules: [later, earlier]);

        Assert.Equal(H(6), account.TargetFor(Monday.AddDays(1)));
        Assert.Equal(H(2), account.TargetFor(Monday.AddDays(-6)));
    }

    [Fact]
    public void Default_applies_before_first_version()
    {
        var account = Account(schedules: [new WorkSchedule { ValidFrom = Monday.AddDays(7), Monday = H(6) }]);
        Assert.Equal(H(8), account.TargetFor(Monday));
        Assert.Equal(H(6), account.TargetFor(Monday.AddDays(7)));
    }

    [Fact]
    public void Week_total_sums_all_days() =>
        Assert.Equal(H(40), WorkSchedule.Default.WeekTotal);
}
