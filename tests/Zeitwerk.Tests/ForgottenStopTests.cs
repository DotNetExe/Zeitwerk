using Zeitwerk.Web.Domain;
using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

/// <summary>R16: „Vergessen zu stoppen?“ – nachträgliches Ende für einen zu lange laufenden Timer.</summary>
public class ForgottenStopTests
{
    private readonly TimerHarness _timer = new();

    [Fact]
    public void Not_forgotten_within_ten_hours_on_same_day()
    {
        _timer.SwitchTo(A, "08:00");

        Assert.False(_timer.IsForgotten("18:00"));
    }

    [Fact]
    public void Forgotten_after_ten_hours()
    {
        _timer.SwitchTo(A, "08:00");

        Assert.True(_timer.IsForgotten("18:00:01"));
    }

    [Fact]
    public void Forgotten_after_midnight_even_if_shorter_than_ten_hours()
    {
        _timer.SwitchTo(A, "22:00");

        Assert.False(_timer.IsForgotten("23:55"));
        Assert.True(_timer.IsForgotten("22.10 00:00"));
    }

    [Fact]
    public void Midnight_is_local_time()
    {
        var timer = new TimerHarness(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
        timer.SwitchTo(A, "20:00"); // 22:00 lokal (MESZ)

        Assert.False(timer.IsForgotten("21:55")); // 23:55 lokal
        Assert.True(timer.IsForgotten("22:00"));  // 00:00 lokal
    }

    [Fact]
    public void On_hold_entry_is_not_forgotten()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.Stop("12:02");
        _timer.SwitchTo(B, "12:03");

        Assert.False(_timer.IsForgotten("12:04"));
    }

    [Fact]
    public void Suggestion_without_typical_duration_is_six_pm_on_start_day()
    {
        _timer.SwitchTo(A, "08:30");

        Assert.Equal(T("18:00"), _timer.SuggestForgottenEnd("22.10 07:00"));
    }

    [Fact]
    public void Suggestion_after_six_pm_start_is_one_hour_later()
    {
        _timer.SwitchTo(A, "19:00");

        Assert.Equal(T("20:00"), _timer.SuggestForgottenEnd("22.10 07:00"));
    }

    [Fact]
    public void Suggestion_uses_typical_duration_rounded_up_to_grid()
    {
        _timer.SwitchTo(A, "08:30");

        Assert.Equal(T("16:35"), _timer.SuggestForgottenEnd("22.10 07:00", TimeSpan.FromMinutes(482)));
    }

    [Fact]
    public void Suggestion_is_never_in_the_future()
    {
        _timer.SwitchTo(A, "08:30");

        Assert.Equal(T("18:35"), _timer.SuggestForgottenEnd("18:37", TimeSpan.FromHours(12)));
    }

    [Theory]
    [InlineData("17:32", "Das Ende muss im 5-Minuten-Raster liegen.")]
    [InlineData("08:30", "Das Ende muss nach dem Beginn liegen.")]
    [InlineData("08:00", "Das Ende muss nach dem Beginn liegen.")]
    [InlineData("22.10 07:05", "Das Ende darf nicht in der Zukunft liegen.")]
    public void Invalid_end_is_rejected(string end, string error)
    {
        _timer.SwitchTo(A, "08:30");

        Assert.Equal(error, _timer.ValidateStopAt(end, "22.10 07:02"));
        Assert.Throws<ArgumentException>(() => _timer.StopAt(end, "22.10 07:02"));
        Assert.Equal(["1 08:30-…"], _timer.Timeline);
    }

    [Fact]
    public void Without_open_entry_nothing_can_be_stopped()
    {
        Assert.Equal("Es läuft kein Timer.", _timer.ValidateStopAt("18:00", "22.10 07:00"));
    }

    [Fact]
    public void Stop_at_chosen_end()
    {
        _timer.SwitchTo(A, "08:30", "PROJ-1");
        _timer.StopAt("17:30", "22.10 07:02");

        Assert.Equal(["1 08:30-17:30 PROJ-1"], _timer.Timeline);
        Assert.Null(_timer.Open);
    }

    [Fact]
    public void End_up_to_now_rounded_down_is_allowed()
    {
        _timer.SwitchTo(A, "08:30");

        Assert.Null(_timer.ValidateStopAt("22.10 07:00", "22.10 07:02"));
    }

    [Fact]
    public void Stop_at_end_after_midnight_is_split_R14()
    {
        _timer.SwitchTo(A, "17:30");
        _timer.StopAt("22.10 01:00", "22.10 07:02");

        Assert.Equal(["1 17:30-22.00:00", "1 22.00:00-22.01:00"], _timer.Timeline);
    }

    [Fact]
    public void Restart_starts_same_work_from_now()
    {
        _timer.SwitchTo(A, "17:30", "PROJ-1");
        _timer.StopAt("18:00", "22.10 07:02", restart: true);

        Assert.Equal(["1 17:30-18:00 PROJ-1", "1 22.07:00-… PROJ-1"], _timer.Timeline);
    }

    [Fact]
    public void Restart_after_split_starts_after_last_piece()
    {
        _timer.SwitchTo(A, "17:30");
        _timer.StopAt("22.10 01:00", "22.10 07:02", restart: true);

        Assert.Equal(["1 17:30-22.00:00", "1 22.00:00-22.01:00", "1 22.07:00-…"], _timer.Timeline);
    }

    [Fact]
    public void Restart_at_now_continues_entry_R3()
    {
        _timer.SwitchTo(A, "17:30");
        _timer.StopAt("22.10 07:00", "22.10 07:02", restart: true);

        Assert.Equal(["1 17:30-22.00:00", "1 22.00:00-…"], _timer.Timeline);
    }
}
