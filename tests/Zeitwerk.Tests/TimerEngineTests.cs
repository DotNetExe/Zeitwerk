using static Zeitwerk.Tests.TimerHarness;

namespace Zeitwerk.Tests;

public class TimerEngineTests
{
    private readonly TimerHarness _timer = new();

    [Fact]
    public void R1_start_from_idle_rounds_down()
    {
        _timer.SwitchTo(A, "09:03");

        Assert.Equal(["1 09:00-…"], _timer.Timeline);
    }

    [Fact]
    public void R2_start_never_cuts_into_previous_entry()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.Stop("12:02");
        _timer.SwitchTo(B, "12:03");

        Assert.Equal(["1 09:00-12:05", "2 12:05-…"], _timer.Timeline);
        Assert.True(_timer.Open!.IsOnHold(T("12:03")));
    }

    [Fact]
    public void R3_restart_with_same_work_continues_previous_entry()
    {
        _timer.SwitchTo(A, "09:00", "PROJ-1");
        _timer.Stop("12:02");
        _timer.SwitchTo(A, "12:04", "PROJ-1");

        Assert.Equal(["1 09:00-… PROJ-1"], _timer.Timeline);
    }

    [Fact]
    public void R3_restart_after_gap_creates_new_entry()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.Stop("12:00");
        _timer.SwitchTo(A, "13:02");

        Assert.Equal(["1 09:00-12:00", "1 13:00-…"], _timer.Timeline);
    }

    [Fact]
    public void R3_restart_with_other_activity_creates_new_entry()
    {
        _timer.SwitchTo(A, "09:00", "PROJ-1");
        _timer.Stop("12:02");
        _timer.SwitchTo(A, "12:03", "PROJ-2");

        Assert.Equal(["1 09:00-12:05 PROJ-1", "1 12:05-… PROJ-2"], _timer.Timeline);
    }

    [Fact]
    public void R4_switch_rounds_up_and_next_entry_is_on_hold()
    {
        _timer.SwitchTo(A, "09:03");
        _timer.SwitchTo(B, "09:47");

        Assert.Equal(["1 09:00-09:50", "2 09:50-…"], _timer.Timeline);
        Assert.True(_timer.Open!.IsOnHold(T("09:47")));
        Assert.False(_timer.Open!.IsOnHold(T("09:50")));
    }

    [Fact]
    public void R5_switch_within_tolerance_uses_previous_boundary()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.SwitchTo(B, "09:50:20");

        Assert.Equal(["1 09:00-09:50", "2 09:50-…"], _timer.Timeline);
    }

    [Fact]
    public void R6_switch_while_on_hold_replaces_pending_entry()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.SwitchTo(B, "09:47");
        _timer.SwitchTo(C, "09:48", "PROJ-7");

        Assert.Equal(["1 09:00-09:50", "3 09:50-… PROJ-7"], _timer.Timeline);
    }

    [Fact]
    public void R7_switch_back_while_on_hold_reopens_previous_entry()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.SwitchTo(B, "09:47");
        _timer.SwitchTo(A, "09:48");

        Assert.Equal(["1 09:00-…"], _timer.Timeline);
    }

    [Fact]
    public void R8_switch_right_after_start_replaces_entry_without_zero_length()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.SwitchTo(B, "09:47");
        _timer.SwitchTo(C, "09:50:30");

        Assert.Equal(["1 09:00-09:50", "3 09:50-…"], _timer.Timeline);
    }

    [Fact]
    public void R9_switch_to_same_work_changes_nothing()
    {
        _timer.SwitchTo(A, "09:00", "PROJ-1");
        _timer.SwitchTo(A, "09:47", " PROJ-1 ");

        Assert.Equal(["1 09:00-… PROJ-1"], _timer.Timeline);
    }

    [Fact]
    public void Switch_to_other_activity_of_same_project_is_a_switch()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.SwitchTo(A, "09:47", "PROJ-1");

        Assert.Equal(["1 09:00-09:50", "1 09:50-… PROJ-1"], _timer.Timeline);
    }

    [Fact]
    public void Empty_activity_is_stored_as_null()
    {
        _timer.SwitchTo(A, "09:00", "   ");

        Assert.Null(_timer.Open!.Activity);
    }

    [Fact]
    public void R10_stop_rounds_up()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.Stop("12:02");

        Assert.Equal(["1 09:00-12:05"], _timer.Timeline);
        Assert.Null(_timer.Open);
    }

    [Fact]
    public void R11_stop_while_on_hold_discards_pending_entry()
    {
        _timer.SwitchTo(A, "09:00");
        _timer.SwitchTo(B, "09:47");
        _timer.Stop("09:48");

        Assert.Equal(["1 09:00-09:50"], _timer.Timeline);
    }

    [Fact]
    public void R12_stop_that_rounds_to_start_discards_entry()
    {
        _timer.SwitchTo(A, "09:00:30");
        _timer.Stop("09:00:50");

        Assert.Empty(_timer.Timeline);
    }

    [Fact]
    public void Stop_without_open_entry_changes_nothing()
    {
        _timer.Stop("09:00");

        Assert.Empty(_timer.Timeline);
    }

    [Fact]
    public void Strict_tolerance_rounds_every_late_click_up()
    {
        var timer = new TimerHarness(toleranceSeconds: 0);
        timer.SwitchTo(A, "09:00");
        timer.SwitchTo(B, "09:50:01");

        Assert.Equal(["1 09:00-09:55", "2 09:55-…"], timer.Timeline);
    }
}
