using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Allocations.Events;

namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// One planned line of a budget period: this much (<see cref="AmountCad"/>), against this budget
/// code, because of this (<see cref="Justification"/>). Revenue is planned the same way as
/// expense — a line on a code whose category is Revenue — so a period's planned revenue is the
/// sum of its revenue lines and its planned expense the sum of the rest, resolved at read time
/// from the code's <em>current</em> category. Nothing about the code except its string is
/// snapshotted here: a code's category and name are editable, and a line must follow them.
/// <para>
/// <b>One line per (tenant, period, code), upserted by code.</b> The set handler updates a line
/// that already exists rather than adding a second, and a unique index is the race backstop.
/// <see cref="Code"/> is the immutable code string copied at creation — the same reason
/// <c>BudgetCode.Code</c> is immutable: a code deleted-and-recreated under the same string must
/// still be findable from the line, and <c>IBudgetCodeUsageProbe</c> matches on id <em>or</em>
/// string for exactly that case.
/// </para>
/// <para>
/// <b>Justification is required.</b> This is zero-based budgeting: every line is argued from
/// zero, each period, and a line without its argument is not a plan — it is a number. The
/// justification that used to sit on the code moved here for that reason (see
/// <c>BudgetCodeDetails.Description</c>).
/// </para>
/// <para>
/// No <c>Delete()</c>: removal is a hard delete driven by the synthetic aggregate-deleted journal
/// row, like codes. Bare <see cref="PeriodId"/>/<see cref="BudgetCodeId"/>, no navigations and
/// no database foreign keys — three aggregates, three boundaries (the <c>ParentCodeId</c>
/// precedent). Whether the period allows changes and whether the code is active are the set
/// handler's checks, because the aggregate cannot see either.
/// </para>
/// </summary>
public sealed class BudgetAllocation : AggregateRoot, ITenantScoped
{
    public const int JustificationMaxLength = 1000;

    /// <summary>
    /// Inclusive ceiling, matching <c>numeric(12,2)</c>: ten integer digits, two decimals. The
    /// domain rejects anything larger so the database never has to.
    /// </summary>
    public const decimal AmountMax = 999_999_999.99m;

    private BudgetAllocation()
    {
        // EF Core materialization only.
        Code = null!;
        Justification = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid PeriodId { get; private set; }
    public Guid BudgetCodeId { get; private set; }

    /// <summary>The code string as it was when the line was created; never changed.</summary>
    public string Code { get; private set; }

    /// <summary>Planned amount in Canadian dollars, rounded to cents. Zero is a valid plan.</summary>
    public decimal AmountCad { get; private set; }

    /// <summary>Why this amount, argued from zero. Trimmed, required.</summary>
    public string Justification { get; private set; }

    /// <summary>
    /// Who created and last changed this line, from the access token's <c>sub</c> claim — never
    /// from a request body. Resolved to an email through <c>user_lookup</c> at read time.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The whole input rule, callable before the aggregate exists. Public so the set handler can
    /// run it <em>first</em>: a malformed payload must report its validation error rather than a
    /// not-found or a conflict for a period or code it was never going to reach.
    /// </summary>
    public static Result Validate(decimal? amount, string? justification)
    {
        if (amount is not { } value)
        {
            return Result.Failure(BudgetAllocationErrors.AmountRequired);
        }

        if (value < 0m)
        {
            return Result.Failure(BudgetAllocationErrors.AmountNegative);
        }

        if (value > AmountMax)
        {
            return Result.Failure(BudgetAllocationErrors.AmountTooLarge);
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            return Result.Failure(BudgetAllocationErrors.JustificationRequired);
        }

        if (justification.Trim().Length > JustificationMaxLength)
        {
            return Result.Failure(BudgetAllocationErrors.JustificationTooLong);
        }

        return Result.Success();
    }

    /// <summary>Creates a line. The code string is copied once, here.</summary>
    public static Result<BudgetAllocation> Create(
        Guid tenantId,
        Guid periodId,
        Guid budgetCodeId,
        string code,
        decimal? amount,
        string? justification,
        Guid? actorId)
    {
        var validation = Validate(amount, justification);
        if (validation.IsFailure)
        {
            return Result.Failure<BudgetAllocation>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var allocation = new BudgetAllocation
        {
            TenantId = tenantId,
            PeriodId = periodId,
            BudgetCodeId = budgetCodeId,
            Code = code,
            AmountCad = Round(amount!.Value),
            Justification = justification!.Trim(),
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        allocation.Raise(new BudgetAllocationCreatedDomainEvent(
            allocation.Id, tenantId, periodId, budgetCodeId, code, allocation.AmountCad, actorId));
        return Result.Success(allocation);
    }

    /// <summary>
    /// Rewrites the amount and justification. Always raises the updated event, even when the
    /// values are unchanged: a re-submitted line is still a decision the journal should carry,
    /// and an eventless Modified save is refused by the audit pipeline anyway.
    /// </summary>
    public Result Update(decimal? amount, string? justification, Guid? actorId)
    {
        var validation = Validate(amount, justification);
        if (validation.IsFailure)
        {
            return validation;
        }

        AmountCad = Round(amount!.Value);
        Justification = justification!.Trim();
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BudgetAllocationUpdatedDomainEvent(Id, AmountCad, actorId));
        return Result.Success();
    }

    // Cents, half away from zero — what a bookkeeper expects, and the same rounding
    // Invoice.TotalCad lands in the numeric(12,2) column with.
    private static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
