namespace NorthernLink.Shared.HoursOfService;

public static partial class HosEngine
{
    private static readonly HosBands ProjectionBands = new();

    /// <summary>
    /// Synthesises the plan onto the ledger (off duty from <see cref="TripPlan.OffDutyFromUnix"/>,
    /// default as-of, then each leg with margin applied) and evaluates at the plan's end. Adverse
    /// extensions already declared are never applied to projected time. Exceptions declared after
    /// as-of are ignored.
    /// </summary>
    public static HosProjection Project(DutyLedger ledger, long asOfUnix, TripPlan plan, HosCalendar calendar, PlanningMargin margin)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(calendar);
        margin ??= new PlanningMargin();

        var problems = new List<HosLedgerProblem>();
        var offFrom = plan.OffDutyFromUnix ?? asOfUnix;
        if (offFrom < asOfUnix)
        {
            problems.Add(new HosLedgerProblem("InvalidPlan", "The assumed off-duty start is before as-of.", offFrom));
        }

        var baseLedger = ledger with
        {
            Exceptions = (ledger.Exceptions ?? []).Where(e => e is not null && e.DeclaredAtUnix <= asOfUnix).ToList(),
        };

        var legs = ValidateLegs(plan, offFrom, problems);
        if (problems.Count > 0 || legs.Count == 0)
        {
            if (legs.Count == 0 && problems.Count == 0)
            {
                problems.Add(new HosLedgerProblem("InvalidPlan", "The plan has no legs.", null));
            }

            var evaluation = Evaluate(baseLedger, asOfUnix, calendar, ProjectionBands, asOfUnix);
            return new HosProjection(false, null, null, 0, offFrom, null, evaluation.Today.OffDutyShortfallSeconds, false, evaluation, problems);
        }

        var core = Simulate(baseLedger, asOfUnix, legs, offFrom, plan.BetweenLegs, calendar, margin, counterfactualOffSeconds: 0);

        int? requiredOff = core.Feasible ? 0 : null;
        if (!core.Feasible)
        {
            var now = Evaluate(baseLedger, asOfUnix, calendar, ProjectionBands, asOfUnix);
            var candidates = new SortedSet<int>
            {
                HosLimits.MandatoryConsecutiveOffSeconds,
                HosLimits.Rest24Seconds,
                HosLimits.CycleResetSeconds,
            };
            if (now.Today.OffDutyShortfallSeconds > 0)
            {
                candidates.Add(now.Today.OffDutyShortfallSeconds);
            }

            foreach (var x in candidates)
            {
                var attempt = Simulate(baseLedger, asOfUnix, legs, offFrom, plan.BetweenLegs, calendar, margin, counterfactualOffSeconds: x);
                if (attempt.Feasible)
                {
                    requiredOff = x;
                    break;
                }
            }
        }

        return new HosProjection(
            core.Feasible,
            core.FirstBreach,
            core.FirstBreach?.AtUnix,
            core.MarginApplied,
            offFrom,
            requiredOff,
            core.StillNeeded,
            core.Achievable,
            core.AtPlanEnd,
            problems);
    }

    private sealed record Leg(long StartUnix, IReadOnlyList<PlannedSegment> Segments, string? TripId);

    private static List<Leg> ValidateLegs(TripPlan plan, long offFrom, List<HosLedgerProblem> problems)
    {
        var legs = new List<Leg>();
        var ordered = (plan.Legs ?? []).Where(l => l is not null).OrderBy(l => l.StartUnix).ToList();
        long previousEnd = long.MinValue;
        foreach (var leg in ordered)
        {
            var segments = (leg.Segments ?? []).Where(s => s is not null).ToList();
            if (segments.Count == 0 || segments.Any(s => s.Seconds <= 0 || !Enum.IsDefined(s.Status)))
            {
                problems.Add(new HosLedgerProblem("InvalidPlan", "A leg has no segments, or a segment with a non-positive duration.", leg.StartUnix));
                continue;
            }

            if (leg.StartUnix < offFrom)
            {
                problems.Add(new HosLedgerProblem("InvalidPlan", "A leg starts before the assumed off-duty start.", leg.StartUnix));
                continue;
            }

            var length = segments.Sum(s => (long)s.Seconds);
            if (leg.StartUnix < previousEnd)
            {
                problems.Add(new HosLedgerProblem("InvalidPlan", "Legs overlap.", leg.StartUnix));
                continue;
            }

            previousEnd = leg.StartUnix + length;
            legs.Add(new Leg(leg.StartUnix, segments, leg.TripId));
        }

        return legs;
    }

    private sealed record Simulation(
        bool Feasible,
        HosViolation? FirstBreach,
        int MarginApplied,
        int StillNeeded,
        bool Achievable,
        HosEvaluation AtPlanEnd);

    private static Simulation Simulate(
        DutyLedger ledger,
        long asOf,
        List<Leg> legs,
        long offFrom,
        DutyStatus betweenLegs,
        HosCalendar cal,
        PlanningMargin margin,
        int counterfactualOffSeconds)
    {
        var events = new List<DutyEvent>(ledger.Events ?? []);
        var sequence = events.Count == 0 ? 1 : events.Max(e => e.Sequence) + 1;

        if (counterfactualOffSeconds > 0)
        {
            // "What if the driver had been off for x ending at the first leg's start?" — replace
            // whatever the ledger says in that span with one off-duty run.
            var offStart = legs[0].StartUnix - counterfactualOffSeconds;
            events.RemoveAll(e => e.AtUnix >= offStart && e.AtUnix < legs[0].StartUnix);
            events.Add(new DutyEvent(offStart, DutyStatus.OffDuty, EventSource.System, sequence++, null, "projection: assumed rest"));
        }

        events.Add(new DutyEvent(offFrom, DutyStatus.OffDuty, EventSource.System, sequence++, null, "projection: off duty"));

        var marginApplied = 0;
        long cursor = offFrom;
        var firstDrivingInstants = new List<long>();
        foreach (var leg in legs)
        {
            cursor = Math.Max(cursor, leg.StartUnix);
            var fixedApplied = false;
            foreach (var segment in leg.Segments)
            {
                var seconds = segment.Seconds;
                if (segment.Status is DutyStatus.Driving or DutyStatus.OnDuty)
                {
                    var scaled = Sec((long)seconds * (100 + margin.Percent) / 100);
                    if (segment.Status == DutyStatus.Driving && !fixedApplied)
                    {
                        scaled += margin.FixedSeconds;
                        fixedApplied = true;
                    }

                    marginApplied += scaled - seconds;
                    seconds = scaled;
                }

                if (segment.Status == DutyStatus.Driving && (firstDrivingInstants.Count == 0 || firstDrivingInstants[^1] < leg.StartUnix))
                {
                    firstDrivingInstants.Add(cursor);
                }

                events.Add(new DutyEvent(cursor, segment.Status, EventSource.System, sequence++, null, leg.TripId));
                cursor += seconds;
            }

            events.Add(new DutyEvent(cursor, betweenLegs, EventSource.System, sequence++, null, leg.TripId));
        }

        var planEnd = cursor;
        var projected = ledger with { Events = events };
        var atEnd = Evaluate(projected, planEnd, cal, ProjectionBands, asOf);

        var breaches = new List<HosViolation>();
        foreach (var v in atEnd.Violations)
        {
            if (!v.Suppressed && v.AtUnix >= asOf)
            {
                breaches.Add(v);
            }
        }

        foreach (var instant in firstDrivingInstants)
        {
            var atStart = Evaluate(projected, instant, cal, ProjectionBands, asOf);
            if (atStart.BlockingReasons.Count > 0)
            {
                breaches.Add(BreachFor(atStart, instant, cal));
            }
        }

        var stillNeeded = atEnd.Today.OffDutyShortfallSeconds;
        var achievable = cal.DayEndUnix(atEnd.Today.Date) - planEnd >= stillNeeded;
        if (!achievable && !breaches.Any(b => b.Rule == HosRule.DailyOffDuty10h && b.Date == atEnd.Today.Date))
        {
            breaches.Add(new HosViolation(
                HosRule.DailyOffDuty10h, atEnd.Today.Date, planEnd,
                atEnd.Today.OffDutySeconds, atEnd.Today.DailyOffRequiredSeconds,
                false, null, HosMessages.For(HosRule.DailyOffDuty10h, atEnd.Today.OffDutySeconds, atEnd.Today.DailyOffRequiredSeconds)));
        }

        var first = breaches.OrderBy(b => b.AtUnix).ThenBy(b => (int)b.Rule).FirstOrDefault();
        return new Simulation(first is null, first, marginApplied, stillNeeded, achievable, atEnd);
    }

    private static HosViolation BreachFor(HosEvaluation at, long instant, HosCalendar cal)
    {
        var reason = at.BlockingReasons[0];
        var (rule, figure, limit) = reason switch
        {
            BlockingReason.NoLedger or BlockingReason.RecordsIncomplete => (HosRule.RecordsIncomplete14d, 0, HosLimits.Rest24WindowSeconds),
            BlockingReason.OutOfService => (HosRule.DroveWhileOutOfService, 0, 0),
            BlockingReason.Rest24Expired => (HosRule.Rest24hIn14d, at.Rest24.BestOffSeconds, HosLimits.Rest24Seconds),
            BlockingReason.CycleExhausted => (HosRule.Cycle1_70hIn7d, at.Cycle.OnDutySeconds, at.Cycle.LimitSeconds),
            BlockingReason.DailyDrivingExhausted => (HosRule.DailyDriving13h, at.Today.DrivingSeconds, HosLimits.DailyDrivingSeconds),
            BlockingReason.DailyOnDutyExhausted => (HosRule.DailyOnDuty14h, at.Today.OnDutySeconds, HosLimits.DailyOnDutySeconds),
            BlockingReason.ShiftDrivingExhausted => (HosRule.ShiftDriving13h, at.Shift.DrivingSeconds, at.Shift.DrivingCeilingSeconds),
            BlockingReason.ShiftOnDutyExhausted => (HosRule.ShiftOnDuty14h, at.Shift.OnDutySeconds, at.Shift.OnDutyCeilingSeconds),
            _ => (HosRule.ShiftElapsed16h, at.Shift.ElapsedSeconds, at.Shift.ElapsedCeilingSeconds),
        };

        return new HosViolation(rule, cal.DayOf(instant), instant, figure, limit, false, null, HosMessages.For(rule, figure, limit));
    }
}
