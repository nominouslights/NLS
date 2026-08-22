using NorthernLink.Trips.Application.Schedules.GenerateTrips;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Schedules;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripGeneratorTests
{
    private static readonly HashSet<(DateOnly, TripDirection)> NoExisting = [];

    private static DateOnly[] OutboundDates(IReadOnlyList<TripDraft> drafts) =>
        [.. drafts.Where(d => d.Direction == TripDirection.Outbound).Select(d => d.ServiceDate)];

    [Fact]
    public void Expands_every_matching_day_across_the_horizon()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(7, drafts.Count);
        Assert.Equal(TestPlanning.Monday, drafts[0].ServiceDate);
        Assert.Equal(TestPlanning.Monday.AddDays(6), drafts[^1].ServiceDate);
        Assert.All(drafts, draft => Assert.Equal(TripDirection.Outbound, draft.Direction));
    }

    [Fact]
    public void Skips_days_the_template_does_not_run()
    {
        var template = TestPlanning.CreateTemplate(generationHorizonDays: 7); // weekdays only

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(5, drafts.Count); // Mon–Fri of the week starting Monday
        Assert.DoesNotContain(drafts, draft => draft.ServiceDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    [Fact]
    public void Horizon_bounds_the_expansion()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            generationHorizonDays: 3);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(3, drafts.Count);
        Assert.All(drafts, draft => Assert.InRange(
            draft.ServiceDate, TestPlanning.Monday, TestPlanning.Monday.AddDays(2)));
    }

    [Fact]
    public void Round_trip_template_pairs_outbound_and_return_legs_with_a_shared_key()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(2, drafts.Count);
        var outbound = Assert.Single(drafts, draft => draft.Direction == TripDirection.Outbound);
        var inbound = Assert.Single(drafts, draft => draft.Direction == TripDirection.Inbound);

        Assert.Equal(new TimeOnly(6, 30), outbound.DepartureTime);
        Assert.Equal(new TimeOnly(17, 30), inbound.DepartureTime);
        Assert.Equal(outbound.ServiceDate, inbound.ServiceDate);

        Assert.NotNull(outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
        Assert.Equal($"{template.Id:N}:{TestPlanning.Monday:yyyyMMdd}", outbound.RoundTripKey);
    }

    [Fact]
    public void One_way_template_has_no_round_trip_key()
    {
        var template = TestPlanning.CreateTemplate(daysOfWeek: [DayOfWeek.Monday], generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        var draft = Assert.Single(drafts);
        Assert.Null(draft.RoundTripKey);
    }

    [Fact]
    public void Already_materialized_occurrences_are_skipped()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var existing = new HashSet<(DateOnly, TripDirection)>
        {
            (TestPlanning.Monday, TripDirection.Outbound),
        };

        var drafts = TripGenerator.Generate(template, existing, TestPlanning.Monday);

        var draft = Assert.Single(drafts);
        Assert.Equal(TripDirection.Inbound, draft.Direction);
    }

    [Fact]
    public void Fully_materialized_template_generates_nothing()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var existing = new HashSet<(DateOnly, TripDirection)>
        {
            (TestPlanning.Monday, TripDirection.Outbound),
            (TestPlanning.Monday, TripDirection.Inbound),
        };

        Assert.Empty(TripGenerator.Generate(template, existing, TestPlanning.Monday));
    }

    [Fact]
    public void Inactive_template_generates_nothing()
    {
        var template = TestPlanning.CreateTemplate(active: false);

        Assert.Empty(TripGenerator.Generate(template, NoExisting, TestPlanning.Monday));
    }

    // ----- DaysOfWeek (regression) -----

    [Fact]
    public void DaysOfWeek_generates_exactly_the_configured_weekdays_over_the_horizon()
    {
        // Window [Mon 2026-07-20, +7) => Mon 20, Tue 21, Wed 22, Thu 23, Fri 24, Sat 25, Sun 26.
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(
            new[] { new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22), new DateOnly(2026, 7, 24) },
            OutboundDates(drafts));
        Assert.All(drafts, draft => Assert.Equal(TripDirection.Outbound, draft.Direction));
    }

    [Fact]
    public void DaysOfWeek_round_trip_pairs_each_weekday_with_a_shared_key()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        // 3 weekdays x (outbound + return) = 6 legs.
        Assert.Equal(6, drafts.Count);
        foreach (var date in new[] { new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 22), new DateOnly(2026, 7, 24) })
        {
            var pair = drafts.Where(d => d.ServiceDate == date).ToList();
            Assert.Equal(2, pair.Count);
            var outbound = Assert.Single(pair, d => d.Direction == TripDirection.Outbound);
            var inbound = Assert.Single(pair, d => d.Direction == TripDirection.Inbound);
            Assert.Equal(new TimeOnly(6, 30), outbound.DepartureTime);
            Assert.Equal(new TimeOnly(17, 30), inbound.DepartureTime);
            Assert.NotNull(outbound.RoundTripKey);
            Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
            Assert.Equal(TripGenerator.RoundTripKeyFor(template.Id, date), outbound.RoundTripKey);
        }
    }

    // ----- EveryNDays -----

    [Fact]
    public void EveryNDays_with_future_anchor_lands_on_anchor_and_multiples_and_skips_before_anchor()
    {
        // today Mon 2026-07-20, anchor Wed 2026-07-22, interval 3, window [20, 30).
        var anchor = new DateOnly(2026, 7, 22);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            intervalDays: 3,
            anchorDate: anchor,
            generationHorizonDays: 10);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(
            new[] { anchor, anchor.AddDays(3), anchor.AddDays(6) },
            OutboundDates(drafts));
        // Dates before the anchor (today 20, 21) never emit.
        Assert.DoesNotContain(drafts, d => d.ServiceDate < anchor);
    }

    [Fact]
    public void EveryNDays_with_past_anchor_still_lands_on_the_interval_within_the_window()
    {
        // today Mon 2026-07-20, anchor Tue 2026-07-14 (past), interval 3, window [20, 30).
        // Sequence from anchor: 14,17,20,23,26,29 -> within window: 20,23,26,29.
        var anchor = new DateOnly(2026, 7, 14);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            intervalDays: 3,
            anchorDate: anchor,
            generationHorizonDays: 10);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 7, 20),
                new DateOnly(2026, 7, 23),
                new DateOnly(2026, 7, 26),
                new DateOnly(2026, 7, 29),
            },
            OutboundDates(drafts));
        // Every emitted date is a whole number of intervals from the anchor.
        Assert.All(drafts, d => Assert.Equal(0, (d.ServiceDate.DayNumber - anchor.DayNumber) % 3));
    }

    [Fact]
    public void EveryNDays_skips_dates_that_do_not_divide_evenly()
    {
        // today == anchor 2026-07-20, interval 3, window [20, 30) -> 20,23,26,29 only.
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.EveryNDays,
            intervalDays: 3,
            anchorDate: TestPlanning.Monday,
            generationHorizonDays: 10);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);
        var dates = OutboundDates(drafts);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 7, 20),
                new DateOnly(2026, 7, 23),
                new DateOnly(2026, 7, 26),
                new DateOnly(2026, 7, 29),
            },
            dates);
        foreach (var offBeat in new[] { 21, 22, 24, 25, 27, 28 })
        {
            Assert.DoesNotContain(new DateOnly(2026, 7, offBeat), dates);
        }
    }

    // ----- MonthlyDays -----

    [Fact]
    public void MonthlyDays_lands_on_the_configured_days()
    {
        // today 2026-08-01, days [1,15], window [Aug 1, Aug 21).
        var today = new DateOnly(2026, 8, 1);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [1, 15],
            generationHorizonDays: 20);

        var drafts = TripGenerator.Generate(template, NoExisting, today);

        Assert.Equal(
            new[] { new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15) },
            OutboundDates(drafts));
    }

    [Fact]
    public void MonthlyDays_31_clamps_to_month_end_of_a_30_day_month()
    {
        // September has 30 days; today 2026-09-01, window [Sep 1, Oct 1) -> Sep 30.
        var today = new DateOnly(2026, 9, 1);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [31],
            generationHorizonDays: 30);

        var drafts = TripGenerator.Generate(template, NoExisting, today);

        var date = Assert.Single(OutboundDates(drafts));
        Assert.Equal(new DateOnly(2026, 9, 30), date);
    }

    [Fact]
    public void MonthlyDays_31_clamps_to_february_28_in_a_non_leap_year()
    {
        // 2026 is not a leap year; February has 28 days.
        var today = new DateOnly(2026, 2, 1);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [31],
            generationHorizonDays: 28);

        var drafts = TripGenerator.Generate(template, NoExisting, today);

        var date = Assert.Single(OutboundDates(drafts));
        Assert.Equal(new DateOnly(2026, 2, 28), date);
    }

    [Fact]
    public void MonthlyDays_31_clamps_to_february_29_in_a_leap_year()
    {
        // 2028 is a leap year; February has 29 days.
        var today = new DateOnly(2028, 2, 1);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [31],
            generationHorizonDays: 29);

        var drafts = TripGenerator.Generate(template, NoExisting, today);

        var date = Assert.Single(OutboundDates(drafts));
        Assert.Equal(new DateOnly(2028, 2, 29), date);
    }

    [Fact]
    public void MonthlyDays_two_days_that_clamp_to_the_same_date_do_not_double_emit()
    {
        // Feb 2026 (28 days): both 30 and 31 clamp to Feb 28 -> a single occurrence.
        var today = new DateOnly(2026, 2, 1);
        var template = TestPlanning.CreateTemplate(
            recurrenceKind: ScheduleRecurrenceKind.MonthlyDays,
            daysOfMonth: [30, 31],
            generationHorizonDays: 28);

        var drafts = TripGenerator.Generate(template, NoExisting, today);

        var date = Assert.Single(OutboundDates(drafts));
        Assert.Equal(new DateOnly(2026, 2, 28), date);
    }
}
