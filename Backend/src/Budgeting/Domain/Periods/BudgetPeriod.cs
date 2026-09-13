using System.Globalization;
using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Periods.Events;

namespace NorthernLink.Budgeting.Domain.Periods;

/// <summary>
/// One planning window for zero-based budgeting: exactly one calendar month or one calendar
/// quarter, identified by (granularity, year, ordinal). The inclusive
/// <see cref="StartsOn"/>/<see cref="EndsOn"/> dates and the display <see cref="Label"/>
/// ("FY2026 Q4" / "March 2026") are derived here, never supplied by callers — the "exactly
/// one calendar month/quarter" invariant lives in one place. Periods within a tenant never
/// share a day (a month inside an existing quarter is an overlap); the handler enforces
/// that against the tenant's other periods, with a unique DB index as the race backstop.
/// <para>
/// <b>Lifecycle is forward-only:</b> always created <see cref="PeriodState.Draft"/>, then
/// <see cref="Transition"/> walks Draft → Finalized → Open → InReview → Closed one step at a
/// time. Each step checks only that the period is in the one state it leaves from, so a skipped
/// or repeated step fails with an error naming that rule (<see cref="BudgetPeriodErrors.NotDraft"/>
/// and friends). Transitions are deliberately <em>not</em> gated on the plan being complete —
/// finalizing an empty plan is allowed, and the dashboard's checklist makes the gap visible
/// instead. Tightening that later is a change to the Finalize arm here, nowhere else.
/// </para>
/// <para>
/// The plan is editable in Draft and Open only (<see cref="AllowsPlanChanges"/>). Finalizing
/// signs the plan off; opening re-allows in-period adjustments; review and close freeze it.
/// The allocation handlers ask this property rather than re-deriving the rule from the state.
/// </para>
/// </summary>
public sealed class BudgetPeriod : AggregateRoot, ITenantScoped
{
    /// <summary>Inclusive bounds of <see cref="Year"/>.</summary>
    public const int YearMin = 2020;
    public const int YearMax = 2100;

    /// <summary>Maximum length of the derived <see cref="Label"/>.</summary>
    public const int LabelMaxLength = 32;

    private BudgetPeriod()
    {
        // EF Core materialization only.
        Label = null!;
    }

    public Guid TenantId { get; private set; }
    public PeriodGranularity Granularity { get; private set; }
    public int Year { get; private set; }

    /// <summary>1-12 for a month period, 1-4 for a quarter period.</summary>
    public int Ordinal { get; private set; }

    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public string Label { get; private set; }
    public PeriodState State { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Whether allocation lines may be added, changed or removed: Draft (the plan is being
    /// built) and Open (in-period adjustments). Finalized, InReview and Closed are read-only.
    /// </summary>
    public bool AllowsPlanChanges => State is PeriodState.Draft or PeriodState.Open;

    /// <summary>Creates a Draft period, deriving dates and label from (granularity, year, ordinal).</summary>
    public static Result<BudgetPeriod> Create(Guid tenantId, PeriodGranularity granularity, int year, int ordinal)
    {
        if (year is < YearMin or > YearMax)
        {
            return Result.Failure<BudgetPeriod>(BudgetPeriodErrors.YearOutOfRange);
        }

        var ordinalMax = granularity == PeriodGranularity.Month ? 12 : 4;
        if (ordinal < 1 || ordinal > ordinalMax)
        {
            return Result.Failure<BudgetPeriod>(BudgetPeriodErrors.OrdinalOutOfRange);
        }

        var startMonth = granularity == PeriodGranularity.Month ? ordinal : (ordinal - 1) * 3 + 1;
        var startsOn = new DateOnly(year, startMonth, 1);
        var monthsSpanned = granularity == PeriodGranularity.Month ? 1 : 3;
        var endsOn = startsOn.AddMonths(monthsSpanned).AddDays(-1);

        // Labels match what the Budgeting console has always displayed: quarters are fiscal
        // ("FY2026 Q4"), months are the invariant-culture English month name ("March 2026").
        var label = granularity == PeriodGranularity.Quarter
            ? $"FY{year} Q{ordinal}"
            : $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(ordinal)} {year}";

        var now = DateTimeOffset.UtcNow;
        var period = new BudgetPeriod
        {
            TenantId = tenantId,
            Granularity = granularity,
            Year = year,
            Ordinal = ordinal,
            StartsOn = startsOn,
            EndsOn = endsOn,
            Label = label,
            State = PeriodState.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        period.Raise(new BudgetPeriodCreatedDomainEvent(period.Id, tenantId));
        return Result.Success(period);
    }

    /// <summary>
    /// Moves the period one step forward. The table is the whole rule: each transition names
    /// the state it leaves from and the state it lands in, and anything else is that
    /// transition's own Conflict. On success stamps <see cref="UpdatedAtUtc"/> and raises
    /// <see cref="BudgetPeriodStateChangedDomainEvent"/> carrying both ends of the step.
    /// </summary>
    public Result Transition(PeriodTransition transition, Guid? actorId)
    {
        var (from, to, error) = transition switch
        {
            PeriodTransition.Finalize => (PeriodState.Draft, PeriodState.Finalized, BudgetPeriodErrors.NotDraft),
            PeriodTransition.Open => (PeriodState.Finalized, PeriodState.Open, BudgetPeriodErrors.NotFinalized),
            PeriodTransition.BeginReview => (PeriodState.Open, PeriodState.InReview, BudgetPeriodErrors.NotOpen),
            PeriodTransition.Close => (PeriodState.InReview, PeriodState.Closed, BudgetPeriodErrors.NotInReview),
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, "Unknown period transition."),
        };

        if (State != from)
        {
            return Result.Failure(error);
        }

        State = to;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BudgetPeriodStateChangedDomainEvent(Id, TenantId, from, to, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Whether this period's inclusive date range intersects the given inclusive range —
    /// a same-day boundary counts, so a month inside an existing quarter overlaps with no
    /// special casing (mirror of <c>Contract.Overlaps</c>, minus the open-ended arm: budget
    /// periods are always bounded).
    /// </summary>
    public bool Overlaps(DateOnly otherStart, DateOnly otherEnd) =>
        StartsOn <= otherEnd && otherStart <= EndsOn;
}
