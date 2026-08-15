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
/// Always created <see cref="PeriodState.Draft"/> — Open/Lock transitions are a later story.
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
    /// Whether this period's inclusive date range intersects the given inclusive range —
    /// a same-day boundary counts, so a month inside an existing quarter overlaps with no
    /// special casing (mirror of <c>Contract.Overlaps</c>, minus the open-ended arm: budget
    /// periods are always bounded).
    /// </summary>
    public bool Overlaps(DateOnly otherStart, DateOnly otherEnd) =>
        StartsOn <= otherEnd && otherStart <= EndsOn;
}
