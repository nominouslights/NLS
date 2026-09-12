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
    public void ReturnNextDay_lands_the_inbound_leg_on_the_following_calendar_day()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30), // next morning
            returnNextDay: true,
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(2, drafts.Count);
        var outbound = Assert.Single(drafts, draft => draft.Direction == TripDirection.Outbound);
        var inbound = Assert.Single(drafts, draft => draft.Direction == TripDirection.Inbound);

        Assert.Equal(TestPlanning.Monday, outbound.ServiceDate);
        Assert.Equal(TestPlanning.Monday.AddDays(1), inbound.ServiceDate);

        // Still one shared key, minted off the outbound's date, even though the legs
        // land on different calendar days.
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
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

    // ----- Exceptions: Skip -----

    [Fact]
    public void Skip_removes_both_legs_of_the_occurrence()
    {
        // Weekdays with a same-day return; skipping Wednesday kills its pair entirely.
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);
        var wednesday = new DateOnly(2026, 7, 22);
        template.AddException(wednesday, ScheduleExceptionKind.Skip, null, null, "Treaty Days");

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(8, drafts.Count); // 4 remaining weekdays x 2 legs
        Assert.DoesNotContain(drafts, d => d.ServiceDate == wednesday);
    }

    [Fact]
    public void Skip_on_an_overnight_template_also_removes_the_next_day_return()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true,
            generationHorizonDays: 7);
        template.AddException(TestPlanning.Monday, ScheduleExceptionKind.Skip, null, null, null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        // Neither Monday's outbound nor Tuesday's inbound return exists.
        Assert.Empty(drafts);
    }

    // ----- Exceptions: ExtraRun -----

    [Fact]
    public void ExtraRun_adds_a_one_way_occurrence_on_a_one_way_template()
    {
        // Monday-only template; extra run on Saturday.
        var template = TestPlanning.CreateTemplate(daysOfWeek: [DayOfWeek.Monday], generationHorizonDays: 7);
        var saturday = new DateOnly(2026, 7, 25);
        template.AddException(saturday, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(2, drafts.Count);
        var extra = Assert.Single(drafts, d => d.ServiceDate == saturday);
        Assert.Equal(TripDirection.Outbound, extra.Direction);
        Assert.Equal(new TimeOnly(9, 0), extra.DepartureTime);
        Assert.Null(extra.RoundTripKey); // no return leg -> no pair to price
    }

    [Fact]
    public void ExtraRun_with_a_return_emits_a_same_day_pair_sharing_a_key_even_on_a_one_way_template()
    {
        var template = TestPlanning.CreateTemplate(daysOfWeek: [DayOfWeek.Monday], generationHorizonDays: 7);
        var saturday = new DateOnly(2026, 7, 25);
        template.AddException(
            saturday, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), new TimeOnly(15, 0), null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);
        var pair = drafts.Where(d => d.ServiceDate == saturday).ToList();

        Assert.Equal(2, pair.Count);
        var outbound = Assert.Single(pair, d => d.Direction == TripDirection.Outbound);
        var inbound = Assert.Single(pair, d => d.Direction == TripDirection.Inbound);
        Assert.Equal(new TimeOnly(9, 0), outbound.DepartureTime);
        Assert.Equal(new TimeOnly(15, 0), inbound.DepartureTime);
        Assert.Equal(TripGenerator.RoundTripKeyFor(template.Id, saturday), outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
    }

    [Fact]
    public void ExtraRun_the_day_after_an_overnight_return_emits_one_inbound_not_two()
    {
        // The wedge case: Monday's overnight return lands on Tuesday (Inbound), and an
        // ExtraRun on Tuesday carries its own same-day return (also Tuesday Inbound).
        // Both target the same (date, direction) key — without in-batch dedupe the two
        // drafts would collide on the unique index and fail the whole template's save.
        // Dates iterate ascending, so the overnight return wins deterministically.
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true,
            generationHorizonDays: 7);
        var tuesday = TestPlanning.Monday.AddDays(1);
        template.AddException(
            tuesday, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), new TimeOnly(15, 0), null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        var inbound = Assert.Single(
            drafts, d => d.ServiceDate == tuesday && d.Direction == TripDirection.Inbound);

        // It's the overnight return's leg: the template's return time, keyed off Monday's
        // outbound — not the extra run's 15:00.
        Assert.Equal(new TimeOnly(6, 30), inbound.DepartureTime);
        Assert.Equal(TripGenerator.RoundTripKeyFor(template.Id, TestPlanning.Monday), inbound.RoundTripKey);
    }

    [Fact]
    public void ExtraRun_outbound_still_emitted_when_its_inbound_is_deduped()
    {
        // Same wedge as above — losing its return leg to the overnight return must not
        // suppress the extra run's outbound.
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true,
            generationHorizonDays: 7);
        var tuesday = TestPlanning.Monday.AddDays(1);
        template.AddException(
            tuesday, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), new TimeOnly(15, 0), null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(3, drafts.Count); // Mon outbound, Tue overnight inbound, Tue extra outbound
        var extraOutbound = Assert.Single(
            drafts, d => d.ServiceDate == tuesday && d.Direction == TripDirection.Outbound);
        Assert.Equal(new TimeOnly(9, 0), extraOutbound.DepartureTime);
    }

    [Fact]
    public void ExtraRun_landing_on_a_recurrence_date_does_not_double_emit_and_its_times_win()
    {
        // Monday is already a recurrence date; the extra run's 09:00 replaces the 06:30.
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);
        template.AddException(
            TestPlanning.Monday, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        // Exception times win outright: departure 09:00 and NO return leg, despite the
        // template's own 17:30 return.
        var draft = Assert.Single(drafts);
        Assert.Equal(TestPlanning.Monday, draft.ServiceDate);
        Assert.Equal(TripDirection.Outbound, draft.Direction);
        Assert.Equal(new TimeOnly(9, 0), draft.DepartureTime);
    }

    [Fact]
    public void ExtraRun_outside_the_horizon_window_is_ignored()
    {
        var template = TestPlanning.CreateTemplate(daysOfWeek: [DayOfWeek.Monday], generationHorizonDays: 7);
        template.AddException(
            TestPlanning.Monday.AddDays(30), ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null);
        template.AddException(
            TestPlanning.Monday.AddDays(-3), ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        // Only the Monday recurrence occurrence — both out-of-window extra runs ignored.
        var draft = Assert.Single(drafts);
        Assert.Equal(TestPlanning.Monday, draft.ServiceDate);
        Assert.Equal(new TimeOnly(6, 30), draft.DepartureTime);
    }

    // ----- Exceptions: TimeOverride -----

    [Fact]
    public void TimeOverride_replaces_only_the_set_time_and_falls_back_for_the_other()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);
        template.AddException(
            TestPlanning.Monday, ScheduleExceptionKind.TimeOverride, new TimeOnly(8, 0), null, null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(2, drafts.Count);
        var outbound = Assert.Single(drafts, d => d.Direction == TripDirection.Outbound);
        var inbound = Assert.Single(drafts, d => d.Direction == TripDirection.Inbound);
        Assert.Equal(new TimeOnly(8, 0), outbound.DepartureTime);   // overridden
        Assert.Equal(new TimeOnly(17, 30), inbound.DepartureTime);  // template fallback
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);  // pairing untouched
    }

    [Fact]
    public void TimeOverride_of_both_times_applies_both()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);
        template.AddException(
            TestPlanning.Monday, ScheduleExceptionKind.TimeOverride, new TimeOnly(8, 0), new TimeOnly(19, 0), null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(new TimeOnly(8, 0), Assert.Single(drafts, d => d.Direction == TripDirection.Outbound).DepartureTime);
        Assert.Equal(new TimeOnly(19, 0), Assert.Single(drafts, d => d.Direction == TripDirection.Inbound).DepartureTime);
    }

    [Fact]
    public void TimeOverride_keeps_ReturnNextDay_behavior()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true,
            generationHorizonDays: 7);
        template.AddException(
            TestPlanning.Monday, ScheduleExceptionKind.TimeOverride, new TimeOnly(19, 0), new TimeOnly(5, 30), null);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        var outbound = Assert.Single(drafts, d => d.Direction == TripDirection.Outbound);
        var inbound = Assert.Single(drafts, d => d.Direction == TripDirection.Inbound);
        Assert.Equal(new TimeOnly(19, 0), outbound.DepartureTime);
        Assert.Equal(new TimeOnly(5, 30), inbound.DepartureTime);
        Assert.Equal(TestPlanning.Monday, outbound.ServiceDate);
        Assert.Equal(TestPlanning.Monday.AddDays(1), inbound.ServiceDate); // still next day
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
    }

    // ----- Exceptions: idempotency -----

    [Fact]
    public void Already_materialized_occurrences_are_still_skipped_with_exceptions_present()
    {
        var template = TestPlanning.CreateTemplate(daysOfWeek: [DayOfWeek.Monday], generationHorizonDays: 7);
        var saturday = new DateOnly(2026, 7, 25);
        template.AddException(
            saturday, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), new TimeOnly(15, 0), null);
        template.AddException(
            TestPlanning.Monday, ScheduleExceptionKind.TimeOverride, new TimeOnly(8, 0), null, null);

        var existing = new HashSet<(DateOnly, TripDirection)>
        {
            (TestPlanning.Monday, TripDirection.Outbound),  // override date already generated
            (saturday, TripDirection.Outbound),             // extra run's outbound already generated
        };

        var drafts = TripGenerator.Generate(template, existing, TestPlanning.Monday);

        // Only the extra run's not-yet-materialized return leg remains.
        var draft = Assert.Single(drafts);
        Assert.Equal(saturday, draft.ServiceDate);
        Assert.Equal(TripDirection.Inbound, draft.Direction);
        Assert.Equal(new TimeOnly(15, 0), draft.DepartureTime);
    }
}
