using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class PmScheduleTests
{
    private static readonly DateOnly Today = new(2026, 8, 22);

    [Fact]
    public void No_completion_computes_not_yet_recorded_with_no_due_math()
    {
        var status = PmSchedule.Compute(10_000, 6, lastDone: null, currentOdometerKm: 50_000, Today);

        Assert.Equal(PmDueState.NotYetRecorded, status.State);
        Assert.Null(status.NextDueKm);
        Assert.Null(status.NextDueDate);
        Assert.Null(status.KmRemaining);
        Assert.Null(status.DaysRemaining);
    }

    [Fact]
    public void Km_only_interval_computes_the_km_arm_alone()
    {
        var status = PmSchedule.Compute(
            10_000, intervalMonths: null, new PmLastDone(40_000, new DateOnly(2026, 6, 1)), 45_000, Today);

        Assert.Equal(50_000, status.NextDueKm);
        Assert.Null(status.NextDueDate);
        Assert.Equal(5_000, status.KmRemaining);
        Assert.Null(status.DaysRemaining);
        Assert.Equal(PmDueState.Ok, status.State);
    }

    [Fact]
    public void Months_only_interval_computes_the_calendar_arm_alone()
    {
        var status = PmSchedule.Compute(
            intervalKm: null, 6, new PmLastDone(40_000, new DateOnly(2026, 6, 1)), 45_000, Today);

        Assert.Null(status.NextDueKm);
        Assert.Equal(new DateOnly(2026, 12, 1), status.NextDueDate);
        Assert.Null(status.KmRemaining);
        Assert.Equal(new DateOnly(2026, 12, 1).DayNumber - Today.DayNumber, status.DaysRemaining);
        Assert.Equal(PmDueState.Ok, status.State);
    }

    [Fact]
    public void Km_arm_hitting_first_governs_when_the_calendar_arm_is_still_fine()
    {
        // Due at 50,000 km or 2027-02-01; the odometer has already reached the km arm.
        var status = PmSchedule.Compute(
            10_000, 12, new PmLastDone(40_000, new DateOnly(2026, 2, 1)), 51_000, Today);

        Assert.Equal(PmDueState.Overdue, status.State);
        Assert.Equal(-1_000, status.KmRemaining);
        Assert.True(status.DaysRemaining > PmSchedule.DefaultLeadDays);
    }

    [Fact]
    public void Date_arm_hitting_first_governs_when_the_km_arm_is_still_fine()
    {
        // Due at 50,000 km or 2026-08-01; the date has passed but the odometer barely moved.
        var status = PmSchedule.Compute(
            10_000, 6, new PmLastDone(40_000, new DateOnly(2026, 2, 1)), 41_000, Today);

        Assert.Equal(PmDueState.Overdue, status.State);
        Assert.True(status.KmRemaining > PmSchedule.DefaultLeadKm);
        Assert.Equal(new DateOnly(2026, 8, 1).DayNumber - Today.DayNumber, status.DaysRemaining);
        Assert.True(status.DaysRemaining < 0);
    }

    [Fact]
    public void Exactly_the_lead_km_before_the_km_arm_is_due_soon()
    {
        var status = PmSchedule.Compute(
            10_000, intervalMonths: null, new PmLastDone(40_000, new DateOnly(2026, 6, 1)),
            50_000 - PmSchedule.DefaultLeadKm, Today);

        Assert.Equal(PmDueState.DueSoon, status.State);
        Assert.Equal(PmSchedule.DefaultLeadKm, status.KmRemaining);
    }

    [Fact]
    public void Exactly_the_lead_days_before_the_calendar_arm_is_due_soon()
    {
        // Due 2026-09-21; today 2026-08-22 is exactly 30 days before.
        var status = PmSchedule.Compute(
            intervalKm: null, 6, new PmLastDone(40_000, new DateOnly(2026, 3, 21)), 41_000, Today);

        Assert.Equal(new DateOnly(2026, 9, 21), status.NextDueDate);
        Assert.Equal(PmSchedule.DefaultLeadDays, status.DaysRemaining);
        Assert.Equal(PmDueState.DueSoon, status.State);
    }

    [Fact]
    public void Reaching_the_km_arm_exactly_is_overdue()
    {
        var status = PmSchedule.Compute(
            10_000, intervalMonths: null, new PmLastDone(40_000, new DateOnly(2026, 6, 1)), 50_000, Today);

        Assert.Equal(PmDueState.Overdue, status.State);
        Assert.Equal(0, status.KmRemaining);
    }

    [Fact]
    public void Reaching_the_due_date_exactly_is_overdue()
    {
        // Due exactly 6 months after 2026-02-22 = today.
        var status = PmSchedule.Compute(
            intervalKm: null, 6, new PmLastDone(40_000, new DateOnly(2026, 2, 22)), 41_000, Today);

        Assert.Equal(Today, status.NextDueDate);
        Assert.Equal(0, status.DaysRemaining);
        Assert.Equal(PmDueState.Overdue, status.State);
    }

    [Fact]
    public void Well_inside_both_arms_is_ok()
    {
        var status = PmSchedule.Compute(
            10_000, 6, new PmLastDone(40_000, new DateOnly(2026, 8, 1)), 41_000, Today);

        Assert.Equal(PmDueState.Ok, status.State);
        Assert.Equal(9_000, status.KmRemaining);
        Assert.True(status.DaysRemaining > PmSchedule.DefaultLeadDays);
    }

    [Fact]
    public void A_calendar_arm_past_the_end_of_the_calendar_clamps_instead_of_throwing()
    {
        var status = PmSchedule.Compute(
            intervalKm: null, 12, new PmLastDone(40_000, new DateOnly(9999, 6, 1)), 41_000, Today);

        Assert.Equal(DateOnly.MaxValue, status.NextDueDate);
        Assert.Equal(PmDueState.Ok, status.State);
    }

    [Fact]
    public void A_lead_km_override_narrows_the_due_soon_window()
    {
        // 1,000 km remaining: DueSoon under the 2,000 km default, still Ok with a 500 km lead.
        var status = PmSchedule.Compute(
            5_000, intervalMonths: null, new PmLastDone(40_000, new DateOnly(2026, 8, 1)),
            44_000, Today, leadKm: 500);

        Assert.Equal(1_000, status.KmRemaining);
        Assert.Equal(PmDueState.Ok, status.State);
    }

    [Fact]
    public void A_lead_km_override_governs_when_the_window_is_entered()
    {
        var status = PmSchedule.Compute(
            5_000, intervalMonths: null, new PmLastDone(40_000, new DateOnly(2026, 8, 1)),
            44_500, Today, leadKm: 500);

        Assert.Equal(500, status.KmRemaining);
        Assert.Equal(PmDueState.DueSoon, status.State);
    }

    [Fact]
    public void A_lead_days_override_widens_the_due_soon_window()
    {
        // Due 2026-12-01, 101 days out: Ok under the 30-day default, DueSoon with a 120-day lead.
        var status = PmSchedule.Compute(
            intervalKm: null, 6, new PmLastDone(40_000, new DateOnly(2026, 6, 1)), 41_000, Today,
            leadDays: 120);

        Assert.Equal(PmDueState.DueSoon, status.State);
    }

    [Fact]
    public void Remaining_values_go_negative_once_overdue()
    {
        var status = PmSchedule.Compute(
            10_000, 6, new PmLastDone(40_000, new DateOnly(2025, 12, 1)), 53_000, Today);

        Assert.Equal(PmDueState.Overdue, status.State);
        Assert.Equal(-3_000, status.KmRemaining);
        Assert.Equal(new DateOnly(2026, 6, 1).DayNumber - Today.DayNumber, status.DaysRemaining);
        Assert.True(status.DaysRemaining < 0);
    }

    [Fact]
    public void A_next_due_km_past_int_max_clamps_instead_of_overflowing()
    {
        // A last-done odometer near int.MaxValue plus a large interval must not wrap
        // negative (which would read as instantly overdue) — it clamps to int.MaxValue,
        // the km-arm twin of the AddMonths calendar clamp.
        var status = PmSchedule.Compute(
            1_000_000, intervalMonths: null,
            new PmLastDone(int.MaxValue - 10, new DateOnly(2026, 6, 1)),
            currentOdometerKm: 500_000, Today);

        Assert.Equal(int.MaxValue, status.NextDueKm);
        Assert.Equal(int.MaxValue - 500_000, status.KmRemaining);
        Assert.Equal(PmDueState.Ok, status.State);
    }
}
