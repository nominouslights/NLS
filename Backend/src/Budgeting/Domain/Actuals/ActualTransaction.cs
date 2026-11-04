using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Actuals.Events;

namespace NorthernLink.Budgeting.Domain.Actuals;

/// <summary>
/// One thing that actually happened with money: this much (<see cref="AmountCad"/>), against this
/// budget code, on this day (<see cref="OccurredOn"/>), for this reason
/// (<see cref="Description"/>). The counterpart to <c>BudgetAllocation</c>, which records what
/// was <em>planned</em>; the rollup sets the two side by side.
/// <para>
/// <b>The period is derived from the date, never stored — this is the single most important
/// design point in the slice.</b> There is no <c>PeriodId</c> column and no period foreign key.
/// <c>CreateBudgetPeriodCommandHandler</c> refuses any period that shares a day with an existing
/// one, so within a tenant a date maps to at most one period, and "the period's actuals" is a
/// range query on <c>occurred_on</c> that can never disagree with the period's own dates. The
/// alternative — stamping a period id at creation — would let the two drift the moment anyone
/// corrected a date, and would need a rewrite of every stored row if a period's bounds ever
/// changed. Correcting the date is all it takes to move a transaction between periods, because
/// the date <em>is</em> the only thing that ever said which period it belonged to.
/// </para>
/// <para>
/// <b>Expenses are positive amounts on an Expense code.</b> No sign convention doing double duty:
/// the code's category says which side of the ledger a figure lands on, and the sign says
/// nothing at all. A negative actual is refused rather than quietly read as a refund — a credit
/// note is its own decision, and inventing a meaning for the minus sign here would make every
/// later report ambiguous.
/// </para>
/// <para>
/// <b>Actuals are not frozen by the period lifecycle.</b> <c>BudgetPeriod.AllowsPlanChanges</c>
/// is deliberately <em>not</em> consulted by the handlers behind this aggregate, and that
/// asymmetry with <c>BudgetAllocation</c> is the point rather than an oversight: only the
/// <em>plan</em> is signed off by finalizing and frozen by review and close. Actuals are facts,
/// they keep arriving after a period ends (an invoice dated the 30th lands on the 6th), and a
/// closed period's actuals are exactly what the review is looking at. Refusing them would mean
/// the ledger can never be corrected once anyone pressed Close.
/// </para>
/// <para>
/// <see cref="Code"/> is the code string copied from the code the transaction points at — the
/// same reason <c>BudgetAllocation.Code</c> holds one: a code deleted and recreated under the
/// same string gets a fresh id, and the old rows must still be findable from the string. It is
/// never edited on its own and can never drift from <see cref="BudgetCodeId"/>; the pair moves
/// together, and only when a transaction is re-pointed at a different code — the correction a
/// bookkeeper reaches for most often, a miscoded expense.
/// </para>
/// <para>
/// Bare <see cref="BudgetCodeId"/>, no navigation and no database foreign key — two aggregates,
/// two boundaries, the <c>BudgetAllocation</c> precedent. Which period covers the date and
/// whether the code is retired are the handlers' checks, because the aggregate can see neither.
/// </para>
/// </summary>
public sealed class ActualTransaction : AggregateRoot, ITenantScoped
{
    /// <summary>What the line says it was for. One line on a ledger screen, not an essay.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>
    /// Wide enough for any external system's transaction id (a QuickBooks id is a short numeric
    /// string; the headroom is for whatever follows it).
    /// </summary>
    public const int ExternalReferenceMaxLength = 128;

    /// <summary>
    /// Inclusive ceiling, matching <c>numeric(12,2)</c>: ten integer digits, two decimals. The
    /// domain rejects anything larger so the database never has to. Same ceiling as
    /// <c>BudgetAllocation.AmountMax</c> — a plan and the spend against it share a column type.
    /// </summary>
    public const decimal AmountMax = 999_999_999.99m;

    private ActualTransaction()
    {
        // EF Core materialization only.
        Code = null!;
        Description = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid BudgetCodeId { get; private set; }

    /// <summary>The code string as it was when this transaction was pointed at the code.</summary>
    public string Code { get; private set; }

    /// <summary>Amount in Canadian dollars, rounded to cents. Never negative; see the type remarks.</summary>
    public decimal AmountCad { get; private set; }

    /// <summary>
    /// The day the money moved. The only thing that says which period this belongs to — there is
    /// no stored period id.
    /// </summary>
    public DateOnly OccurredOn { get; private set; }

    /// <summary>What it was for. Trimmed, required.</summary>
    public string Description { get; private set; }

    /// <summary>
    /// Manual or QuickBooks. Fixed at creation: a row's origin is a fact about how it got here,
    /// and rewriting it would make the re-import guard lie about what it is protecting.
    /// </summary>
    public ActualTransactionSource Source { get; private set; }

    /// <summary>
    /// The originating system's own id for this transaction, when there is one — null for
    /// everything entered by hand. Unique per (tenant, source) when set, which is what makes a
    /// re-run of an import idempotent rather than doubling every figure on the report.
    /// </summary>
    public string? ExternalReference { get; private set; }

    /// <summary>
    /// Who recorded and last changed this transaction, from the access token's <c>sub</c> claim —
    /// never from a request body. Resolved to an email through <c>user_lookup</c> at read time.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The whole input rule, callable before the aggregate exists. Public so the handlers can run
    /// it <em>first</em>: a malformed payload must report its validation error rather than a
    /// not-found for a code, or a conflict about a period, it was never going to reach.
    /// <para>
    /// There are no calendar bounds on <paramref name="occurredOn"/> on purpose. The real gate is
    /// that some period covers the date, and that check lives in the handler because the
    /// aggregate cannot see periods — a year range duplicated here would be a second, weaker
    /// version of the same rule, drifting the moment <c>BudgetPeriod.YearMin</c> moved.
    /// </para>
    /// </summary>
    public static Result Validate(decimal? amount, DateOnly? occurredOn, string? description)
    {
        if (amount is not { } value)
        {
            return Result.Failure(ActualTransactionErrors.AmountRequired);
        }

        if (value < 0m)
        {
            return Result.Failure(ActualTransactionErrors.AmountNegative);
        }

        if (value > AmountMax)
        {
            return Result.Failure(ActualTransactionErrors.AmountTooLarge);
        }

        if (occurredOn is null)
        {
            return Result.Failure(ActualTransactionErrors.OccurredOnRequired);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure(ActualTransactionErrors.DescriptionRequired);
        }

        if (description.Trim().Length > DescriptionMaxLength)
        {
            return Result.Failure(ActualTransactionErrors.DescriptionTooLong);
        }

        return Result.Success();
    }

    /// <summary>
    /// Records a transaction. The code string is copied here from the code it points at.
    /// <paramref name="externalReference"/> is blank-to-null so an importer that hands over an
    /// empty string does not create a row that looks referenced but collides with nothing.
    /// </summary>
    public static Result<ActualTransaction> Create(
        Guid tenantId,
        Guid budgetCodeId,
        string code,
        decimal? amount,
        DateOnly? occurredOn,
        string? description,
        ActualTransactionSource source,
        string? externalReference,
        Guid? actorId)
    {
        var validation = Validate(amount, occurredOn, description);
        if (validation.IsFailure)
        {
            return Result.Failure<ActualTransaction>(validation.Error);
        }

        var reference = Normalize(externalReference);
        if (reference?.Length > ExternalReferenceMaxLength)
        {
            return Result.Failure<ActualTransaction>(ActualTransactionErrors.ExternalReferenceTooLong);
        }

        var now = DateTimeOffset.UtcNow;
        var transaction = new ActualTransaction
        {
            TenantId = tenantId,
            BudgetCodeId = budgetCodeId,
            Code = code,
            AmountCad = Round(amount!.Value),
            OccurredOn = occurredOn!.Value,
            Description = description!.Trim(),
            Source = source,
            ExternalReference = reference,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        transaction.Raise(new ActualTransactionCreatedDomainEvent(
            transaction.Id,
            tenantId,
            budgetCodeId,
            code,
            transaction.AmountCad,
            transaction.OccurredOn,
            source,
            actorId));

        return Result.Success(transaction);
    }

    /// <summary>
    /// Rewrites everything a correction can touch: which code the spend belongs to, the amount,
    /// the date and the description. <see cref="Source"/> and <see cref="ExternalReference"/> are
    /// not among them — those say where the row came from, not what it claims.
    /// <para>
    /// The (id, string) code pair is re-snapshotted together, never separately, so
    /// <see cref="Code"/> still always reads as the string of the code this row points at. That
    /// is the same guarantee <c>BudgetAllocation.Code</c> gives; an allocation line simply never
    /// needs it re-taken, because there the (period, code) pair is the line's identity, while a
    /// transaction is identified by its own id and miscoding one is an everyday mistake to fix.
    /// </para>
    /// <para>
    /// Always raises the updated event, even when nothing changed: a re-submitted correction is
    /// still a decision the journal should carry, and an eventless Modified save is refused by
    /// the audit pipeline anyway.
    /// </para>
    /// </summary>
    public Result Update(
        Guid budgetCodeId,
        string code,
        decimal? amount,
        DateOnly? occurredOn,
        string? description,
        Guid? actorId)
    {
        var validation = Validate(amount, occurredOn, description);
        if (validation.IsFailure)
        {
            return validation;
        }

        BudgetCodeId = budgetCodeId;
        Code = code;
        AmountCad = Round(amount!.Value);
        OccurredOn = occurredOn!.Value;
        Description = description!.Trim();
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ActualTransactionUpdatedDomainEvent(Id, budgetCodeId, code, AmountCad, OccurredOn, actorId));
        return Result.Success();
    }

    // Blank is not a reference. Trimmed so a trailing newline out of an import cannot make two
    // copies of the same id look like two different transactions to the unique index.
    private static string? Normalize(string? externalReference) =>
        string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();

    // Cents, half away from zero — what a bookkeeper expects, and the same rounding
    // BudgetAllocation.AmountCad and Invoice.TotalCad land in numeric(12,2) with.
    private static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
