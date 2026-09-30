using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Tests;

/// <summary>Shared builders for Budgeting tests.</summary>
internal static class TestBudgeting
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A stand-in for an authenticated user id, for the created_by/modified_by paths.</summary>
    public static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// A valid set of code details, with every field overridable per test. Defaults are the
    /// required-only shape plus a service line, so a test that cares about one field does not
    /// have to restate the rest.
    /// </summary>
    public static BudgetCodeDetails CodeDetails(
        string name = "Alamos crew shuttle",
        BudgetCodeCategory category = BudgetCodeCategory.Revenue,
        BudgetReviewFrequency reviewFrequency = BudgetReviewFrequency.Quarterly,
        BudgetServiceLine? serviceLine = BudgetServiceLine.ContractCrew,
        string? costCentre = null,
        Guid? parentCodeId = null,
        string? glAccountCode = null,
        BudgetTaxTreatment? taxTreatment = null,
        Guid? budgetOwnerUserId = null,
        string? description = "Contracted crew rotation runs under the Alamos master agreement.") => new()
        {
            Name = name,
            Category = category,
            ReviewFrequency = reviewFrequency,
            ServiceLine = serviceLine,
            CostCentre = costCentre,
            ParentCodeId = parentCodeId,
            GlAccountCode = glAccountCode,
            TaxTreatment = taxTreatment,
            BudgetOwnerUserId = budgetOwnerUserId,
            Description = description,
        };

    /// <summary>
    /// The period a test code lands in when a test does not name one. Codes belong to a period;
    /// tests about a code's own rules only need it to be <em>some</em> period, so they share this
    /// one — and a test about periods passes its own.
    /// </summary>
    public static readonly Guid DefaultPeriodId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static BudgetCode CreateCode(
        string code = "ZBB-CREW-01",
        BudgetCodeDetails? details = null,
        Guid? actorId = null,
        Guid? periodId = null)
    {
        var result = BudgetCode.Create(TenantId, periodId ?? DefaultPeriodId, code, details ?? CodeDetails(), actorId);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test budget code invalid: {result.Error.Code}");
        }

        return result.Value;
    }

    public static BudgetPeriod CreatePeriod(
        PeriodGranularity granularity = PeriodGranularity.Quarter,
        int year = 2026,
        int ordinal = 4)
    {
        var result = BudgetPeriod.Create(TenantId, granularity, year, ordinal);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test period invalid: {result.Error.Code}");
        }

        return result.Value;
    }

    /// <summary>
    /// The forward order of the lifecycle, one step per edge. Tests that need "every transition"
    /// iterate this rather than <c>Enum.GetValues</c>, so the walk in <see cref="PeriodIn"/> and
    /// the tables in the tests agree on the order by construction.
    /// </summary>
    public static readonly PeriodTransition[] TransitionsInOrder =
    [
        PeriodTransition.Finalize,
        PeriodTransition.Open,
        PeriodTransition.BeginReview,
        PeriodTransition.Close,
    ];

    /// <summary>
    /// A period already in <paramref name="target"/>, reached by walking the real transitions
    /// from Draft — no back door onto <c>State</c>, so a test in a late state also exercises
    /// the path there. Throws if a step is refused (that is a domain bug, not a test-setup
    /// choice). The walk's domain events are cleared before returning, so the period looks like
    /// one loaded from the database and a test sees only the events it raises itself.
    /// </summary>
    public static BudgetPeriod PeriodIn(
        PeriodState target,
        PeriodGranularity granularity = PeriodGranularity.Quarter,
        int year = 2026,
        int ordinal = 4)
    {
        var period = CreatePeriod(granularity, year, ordinal);
        foreach (var transition in TransitionsInOrder)
        {
            if (period.State == target)
            {
                break;
            }

            var step = period.Transition(transition, ActorId);
            if (step.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Walking to {target} failed at {transition} from {period.State}: {step.Error.Code}");
            }
        }

        if (period.State != target)
        {
            throw new InvalidOperationException($"Walking every transition never reached {target}.");
        }

        period.ClearDomainEvents();
        return period;
    }

    /// <summary>
    /// A valid lump-sum budget item's details, every field overridable. Defaults are the
    /// required-only shape (title, amount, justification) plus the documented enum defaults, so
    /// a test that cares about one field does not have to restate the rest.
    /// </summary>
    public static BudgetItemDetails Item(
        string? title = "Crew rotations, Q4",
        decimal? amount = 1250.00m,
        string? justification = "Two crew rotations a week under the Alamos master agreement.",
        decimal? quantity = null,
        decimal? unitCost = null,
        string? unit = null,
        BudgetSpendType spendType = BudgetSpendType.Operating,
        BudgetRecurrence recurrence = BudgetRecurrence.OneTime,
        string? vendor = null,
        IReadOnlyList<string>? tags = null,
        BudgetItemPriority priority = BudgetItemPriority.ShouldHave,
        string? assumptions = null,
        string? consequence = null) => new()
        {
            Title = title,
            AmountCad = amount,
            Justification = justification,
            Quantity = quantity,
            UnitCostCad = unitCost,
            Unit = unit,
            SpendType = spendType,
            Recurrence = recurrence,
            Vendor = vendor,
            Tags = tags,
            Priority = priority,
            Assumptions = assumptions,
            ConsequenceIfUnfunded = consequence,
        };

    /// <summary>
    /// A valid budget item. The period and code ids default to fresh guids because the
    /// aggregate holds bare ids — a test that needs the item to point at a real period or code
    /// passes theirs in. <paramref name="details"/>, when given, wins over
    /// <paramref name="amount"/>/<paramref name="justification"/>; <paramref name="tenantId"/>
    /// exists for the tenant-isolation tests.
    /// </summary>
    public static BudgetAllocation CreateAllocation(
        Guid? periodId = null,
        Guid? budgetCodeId = null,
        string code = "ZBB-CREW-01",
        decimal? amount = 1250.00m,
        string? justification = "Two crew rotations a week under the Alamos master agreement.",
        Guid? actorId = null,
        BudgetItemDetails? details = null,
        Guid? tenantId = null)
    {
        var result = BudgetAllocation.Create(
            tenantId ?? TenantId,
            periodId ?? Guid.NewGuid(),
            budgetCodeId ?? Guid.NewGuid(),
            code,
            details ?? Item(amount: amount, justification: justification),
            actorId);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test allocation invalid: {result.Error.Code}");
        }

        return result.Value;
    }
}
