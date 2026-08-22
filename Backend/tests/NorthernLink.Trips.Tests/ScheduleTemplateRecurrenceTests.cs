using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Validation + field-hygiene coverage for the three <see cref="ScheduleRecurrenceKind"/> models on
/// <see cref="ScheduleTemplate"/>: each kind's guard rejects bad input, and ApplyRecurrence keeps
/// only the selected kind's fields populated.
/// </summary>
public class ScheduleTemplateRecurrenceTests
{
    // ----- DaysOfWeek guard -----

    [Fact]
    public void DaysOfWeek_with_no_days_fails_with_AtLeastOneDay()
    {
        var result = TestPlanning.CreateTemplateResult(
            recurrenceKind: ScheduleRecurrenceKind.DaysOfWeek,
            daysOfWeek: []);

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.AtLeastOneDay, result.Error);
    }

    // ----- EveryNDays guard -----

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    [InlineData(-1)]
    public void EveryNDays_with_out_of_range_interval_fails_with_InvalidInterval(int interval)
    {
        var result = TestPlanning.CreateTemplateResult(
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            intervalDays: interval,
            anchorDate: new DateOnly(2026, 7, 20));

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.InvalidInterval, result.Error);
    }

    [Fact]
    public void EveryNDays_with_null_anchor_fails_with_AnchorRequired()
    {
        var result = TestPlanning.CreateTemplateResult(
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            intervalDays: 3,
            anchorDate: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.AnchorRequired, result.Error);
    }

    // ----- MonthlyDays guard -----

    [Fact]
    public void MonthlyDays_with_no_days_fails_with_AtLeastOneDayOfMonth()
    {
        var result = TestPlanning.CreateTemplateResult(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: []);

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.AtLeastOneDayOfMonth, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void MonthlyDays_with_out_of_range_day_fails_with_InvalidDayOfMonth(int day)
    {
        var result = TestPlanning.CreateTemplateResult(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [day]);

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.InvalidDayOfMonth, result.Error);
    }

    // ----- ApplyRecurrence field hygiene -----

    [Fact]
    public void Create_as_EveryNDays_leaves_only_interval_fields_populated()
    {
        var anchor = new DateOnly(2026, 7, 20);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            intervalDays: 5,
            anchorDate: anchor);

        Assert.Equal(ScheduleRecurrenceKind.EveryNDays, template.RecurrenceKind);
        Assert.Equal(5, template.IntervalDays);
        Assert.Equal(anchor, template.AnchorDate);
        Assert.Empty(template.DaysOfWeek);
        Assert.Empty(template.DaysOfMonth);
    }

    [Fact]
    public void Create_as_MonthlyDays_leaves_only_month_days_populated()
    {
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [1, 15]);

        Assert.Equal(ScheduleRecurrenceKind.MonthlyDays, template.RecurrenceKind);
        Assert.Equal([1, 15], template.DaysOfMonth);
        Assert.Empty(template.DaysOfWeek);
        Assert.Null(template.IntervalDays);
        Assert.Null(template.AnchorDate);
    }

    [Fact]
    public void Switching_a_DaysOfWeek_template_to_EveryNDays_clears_the_other_kinds_fields()
    {
        // Starts as a weekday template with a populated DaysOfWeek list.
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Friday]);
        Assert.NotEmpty(template.DaysOfWeek);

        var anchor = new DateOnly(2026, 8, 1);
        var update = template.Update(
            name: "Alamos crew shuttle",
            routeId: Guid.NewGuid(),
            serviceType: TripServiceType.ContractCrew,
            clientId: null,
            clientName: "Alamos Gold",
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Friday], // supplied but must be ignored/cleared
            intervalDays: 4,
            anchorDate: anchor,
            daysOfMonth: [1, 2], // supplied but must be ignored/cleared
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: null,
            seatsCapacity: 12,
            seatsMinimum: null,
            defaultVehicleUnit: "U-04",
            defaultDriverId: null,
            generationHorizonDays: 7,
            cutoffNote: null);

        Assert.True(update.IsSuccess);
        Assert.Equal(ScheduleRecurrenceKind.EveryNDays, template.RecurrenceKind);
        Assert.Equal(4, template.IntervalDays);
        Assert.Equal(anchor, template.AnchorDate);
        Assert.Empty(template.DaysOfWeek);
        Assert.Empty(template.DaysOfMonth);
    }
}
