using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Allocations.Events;

namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// One <b>budget item</b> of a budget period — a small zero-based decision package: this much
/// (<see cref="AmountCad"/>), for this (<see cref="Title"/>), against this budget code, because of
/// this (<see cref="Justification"/>), ranked this highly (<see cref="Priority"/>). The console
/// calls these "budget items"; the aggregate and the <c>allocations</c> route keep their original
/// name to spare the churn.
/// <para>
/// <b>A code's budget in a period is the sum of its items, and a period may hold any number of
/// items per code.</b> Nothing stores a code-level figure: the period's planned revenue is the sum
/// of its items on Revenue codes and its planned expense the sum of the rest, resolved at read time
/// from the code's <em>current</em> category. Nothing about the code except its string is
/// snapshotted here: a code's category and name are editable, and an item must follow them.
/// </para>
/// <para>
/// <b>Items are addressed by their own id.</b> (The one-line-per-code upsert this replaced keyed
/// everything by (period, code).) <see cref="Code"/> is the code string copied whenever the item
/// is assigned to a code — at creation, and again when an update moves it to another code — so a
/// code deleted-and-recreated under the same string is still findable from the item, and
/// <c>IBudgetCodeUsageProbe</c> matches on id <em>or</em> string for exactly that case.
/// </para>
/// <para>
/// <b>The amount is computed when the item is built up.</b> Given a <see cref="Quantity"/> and a
/// <see cref="UnitCostCad"/> (both or neither), <see cref="AmountCad"/> is
/// <c>round(quantity × unit cost, 2, AwayFromZero)</c> and any amount the caller sent is ignored;
/// otherwise the amount is a required lump sum. The platform never computes tax — there is no tax
/// field, and the amount is a plain figure.
/// </para>
/// <para>
/// <b>Justification is required.</b> This is zero-based budgeting: every item is argued from
/// zero, each period, and an item without its argument is not a plan — it is a number. The single
/// exception is <see cref="CopyInto"/>, which carries an earlier period's item forward with an
/// <em>empty</em> justification on purpose — read its doc comment before touching it.
/// </para>
/// <para>
/// No <c>Delete()</c>: removal is a hard delete driven by the synthetic aggregate-deleted journal
/// row, like codes. Bare <see cref="PeriodId"/>/<see cref="BudgetCodeId"/>, no navigations and
/// no database foreign keys — three aggregates, three boundaries (the <c>ParentCodeId</c>
/// precedent). Whether the period allows changes and whether the code is active are the
/// handlers' checks, because the aggregate cannot see either. Items follow the period lifecycle
/// only — there is no per-item approval.
/// </para>
/// </summary>
public sealed class BudgetAllocation : AggregateRoot, ITenantScoped
{
    public const int TitleMaxLength = 120;
    public const int JustificationMaxLength = 1000;
    public const int UnitMaxLength = 32;
    public const int VendorMaxLength = 120;
    public const int AssumptionsMaxLength = 1000;
    public const int ConsequenceMaxLength = 1000;
    public const int MaxTags = 10;
    public const int TagMaxLength = 32;

    /// <summary>
    /// Inclusive ceiling, matching <c>numeric(12,2)</c>: ten integer digits, two decimals. The
    /// domain rejects anything larger — amount, quantity or unit cost — so the database never has to.
    /// </summary>
    public const decimal AmountMax = 999_999_999.99m;

    private BudgetAllocation()
    {
        // EF Core materialization only.
        Code = null!;
        Title = null!;
        Justification = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid PeriodId { get; private set; }
    public Guid BudgetCodeId { get; private set; }

    /// <summary>The code string as it was when the item was assigned to its current code.</summary>
    public string Code { get; private set; }

    /// <summary>What the money is for. Trimmed, required, at most <see cref="TitleMaxLength"/>.</summary>
    public string Title { get; private set; }

    /// <summary>
    /// Planned amount in Canadian dollars, rounded to cents. Zero is a valid plan. Computed from
    /// <see cref="Quantity"/> × <see cref="UnitCostCad"/> when both are set.
    /// </summary>
    public decimal AmountCad { get; private set; }

    /// <summary>How many units, rounded to hundredths; null for a lump sum. Set iff <see cref="UnitCostCad"/> is.</summary>
    public decimal? Quantity { get; private set; }

    /// <summary>Cost per unit, rounded to cents; null for a lump sum. Set iff <see cref="Quantity"/> is.</summary>
    public decimal? UnitCostCad { get; private set; }

    /// <summary>What one unit is. Always null when <see cref="Quantity"/> is.</summary>
    public string? Unit { get; private set; }

    /// <summary>Why this amount, argued from zero. Trimmed, required (empty only on a copy).</summary>
    public string Justification { get; private set; }

    public BudgetSpendType SpendType { get; private set; }

    public BudgetRecurrence Recurrence { get; private set; }

    public string? Vendor { get; private set; }

    /// <summary>Trimmed, de-duplicated case-insensitively (first spelling wins), at most <see cref="MaxTags"/>.</summary>
    public List<string> Tags { get; private set; } = [];

    public BudgetItemPriority Priority { get; private set; }

    public string? Assumptions { get; private set; }

    public string? ConsequenceIfUnfunded { get; private set; }

    /// <summary>
    /// Whether this item is still waiting for its argument — true only for an item produced by
    /// <see cref="CopyInto"/>, which is the one path that stores an empty justification.
    /// <para>
    /// Get-only with no backing field, so EF Core never maps it — exactly the reason
    /// <c>BudgetPeriod.AllowsPlanChanges</c> needs no column either. <b>Adding a setter or a
    /// backing field here would make it a mapped property and demand a migration</b> for a value
    /// that is a pure function of <see cref="Justification"/>.
    /// </para>
    /// </summary>
    public bool NeedsJustification => Justification.Length == 0;

    /// <summary>
    /// Who created and last changed this item, from the access token's <c>sub</c> claim — never
    /// from a request body. Resolved to an email through <c>user_lookup</c> at read time.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The whole input rule, callable before the aggregate exists. Public so the handlers can run
    /// it <em>first</em>: a malformed payload must report its validation error rather than a
    /// not-found or a conflict for a period or code it was never going to reach. One error at a
    /// time, in this order: title, cost (quantity / unit cost / amount), justification, the three
    /// enums, unit, vendor, tags, assumptions, consequence.
    /// </summary>
    public static Result Validate(BudgetItemDetails details)
    {
        var parsed = Parse(details);
        return parsed.IsSuccess ? Result.Success() : Result.Failure(parsed.Error);
    }

    /// <summary>Creates an item. The code string is copied here.</summary>
    public static Result<BudgetAllocation> Create(
        Guid tenantId,
        Guid periodId,
        Guid budgetCodeId,
        string code,
        BudgetItemDetails details,
        Guid? actorId)
    {
        var parsed = Parse(details);
        if (parsed.IsFailure)
        {
            return Result.Failure<BudgetAllocation>(parsed.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var allocation = new BudgetAllocation
        {
            TenantId = tenantId,
            PeriodId = periodId,
            BudgetCodeId = budgetCodeId,
            Code = code,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        allocation.Apply(parsed.Value);

        allocation.Raise(new BudgetAllocationCreatedDomainEvent(
            allocation.Id, tenantId, periodId, budgetCodeId, code, allocation.Title, allocation.AmountCad, actorId));
        return Result.Success(allocation);
    }

    /// <summary>
    /// Copies this item into another period, <b>carrying every field and deliberately dropping
    /// the argument</b>: same tenant, same budget code (id and string), same title, amount, cost
    /// build-up, classification, vendor, tags, priority, assumptions and consequence — and
    /// <see cref="Justification"/> set to <see cref="string.Empty"/>.
    /// <para>
    /// <b>This is the one place the aggregate's headline invariant is broken on purpose, and that
    /// is the feature — do not "fix" it by routing through <see cref="Create"/>.</b>
    /// <see cref="Validate"/> rejects an empty justification, so <see cref="Create"/> physically
    /// cannot produce a copied item. Zero-based budgeting means the item may be seeded from an
    /// earlier period as a <em>starting position</em>, but the argument for it is never inherited:
    /// last quarter's reasoning is not this quarter's reasoning, and a copy that brought the
    /// justification along would turn ZBB into rollover budgeting with extra steps. The
    /// assumptions and the consequence-if-unfunded <em>do</em> come across: they are working
    /// notes a planner re-checks, not the argument itself.
    /// </para>
    /// <para>
    /// The copied item is therefore <see cref="NeedsJustification"/> until somebody argues it, and
    /// the update handler — which runs <see cref="Validate"/> first — will refuse to save it again
    /// with <c>JustificationRequired</c> until they do. The empty string is legal in the database
    /// (<c>justification varchar(1000) NOT NULL</c>).
    /// </para>
    /// <para>
    /// An <em>instance</em> method rather than a static factory, so the copy cannot be handed a
    /// mismatched tenant, code id or code string — they come from the item being copied, and only
    /// the period and the actor are supplied. Nothing here can fail: every field was already
    /// validated and rounded when the source item was written. Raises
    /// <see cref="BudgetAllocationCreatedDomainEvent"/> exactly as <see cref="Create"/> does.
    /// </para>
    /// </summary>
    public BudgetAllocation CopyInto(Guid periodId, Guid? actorId)
    {
        var now = DateTimeOffset.UtcNow;
        var copy = new BudgetAllocation
        {
            TenantId = TenantId,
            PeriodId = periodId,
            BudgetCodeId = BudgetCodeId,
            Code = Code,
            Title = Title,
            // Already rounded when the source item was written; carried straight across.
            AmountCad = AmountCad,
            Quantity = Quantity,
            UnitCostCad = UnitCostCad,
            Unit = Unit,
            Justification = string.Empty,
            SpendType = SpendType,
            Recurrence = Recurrence,
            Vendor = Vendor,
            Tags = [.. Tags],
            Priority = Priority,
            Assumptions = Assumptions,
            ConsequenceIfUnfunded = ConsequenceIfUnfunded,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        copy.Raise(new BudgetAllocationCreatedDomainEvent(
            copy.Id, TenantId, periodId, BudgetCodeId, Code, Title, AmountCad, actorId));
        return copy;
    }

    /// <summary>
    /// Rewrites the item, including (optionally) moving it to another budget code — the handler
    /// has already checked that code exists and is active. The period never changes. Always raises
    /// the updated event, even when nothing changed: a re-submitted item is still a decision the
    /// journal should carry, and an eventless Modified save is refused by the audit pipeline anyway.
    /// A refused update changes nothing and raises nothing.
    /// </summary>
    public Result Update(Guid budgetCodeId, string code, BudgetItemDetails details, Guid? actorId)
    {
        var parsed = Parse(details);
        if (parsed.IsFailure)
        {
            return Result.Failure(parsed.Error);
        }

        BudgetCodeId = budgetCodeId;
        Code = code;
        Apply(parsed.Value);
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BudgetAllocationUpdatedDomainEvent(Id, BudgetCodeId, Code, Title, AmountCad, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Cents (and hundredths of a unit), half away from zero — what a bookkeeper expects, and the
    /// same rounding <c>Invoice.TotalCad</c> lands in the numeric(12,2) column with. Public so the
    /// console's mirror of the computed amount can name the rule it copies.
    /// </summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private void Apply(ParsedItem item)
    {
        Title = item.Title;
        AmountCad = item.AmountCad;
        Quantity = item.Quantity;
        UnitCostCad = item.UnitCostCad;
        Unit = item.Unit;
        Justification = item.Justification;
        SpendType = item.SpendType;
        Recurrence = item.Recurrence;
        Vendor = item.Vendor;
        Tags = [.. item.Tags];
        Priority = item.Priority;
        Assumptions = item.Assumptions;
        ConsequenceIfUnfunded = item.ConsequenceIfUnfunded;
    }

    /// <summary>Validates and normalizes in one pass, so what is checked is exactly what is stored.</summary>
    private static Result<ParsedItem> Parse(BudgetItemDetails details)
    {
        // --- What ---
        if (string.IsNullOrWhiteSpace(details.Title))
        {
            return Fail(BudgetAllocationErrors.TitleRequired);
        }

        var title = details.Title.Trim();
        if (title.Length > TitleMaxLength)
        {
            return Fail(BudgetAllocationErrors.TitleTooLong);
        }

        // --- Cost: built up (quantity × unit cost) or a lump sum ---
        decimal amount;
        decimal? quantity = null;
        decimal? unitCost = null;

        if (details.Quantity.HasValue != details.UnitCostCad.HasValue)
        {
            return Fail(BudgetAllocationErrors.QuantityWithoutUnitCost);
        }

        if (details.Quantity is { } rawQuantity && details.UnitCostCad is { } rawUnitCost)
        {
            // Rounded before the checks, so a quantity that would land in numeric(12,2) as 0.00
            // (0.004) is refused as not positive rather than stored as a zero-unit item.
            var q = Round(rawQuantity);
            if (q <= 0m)
            {
                return Fail(BudgetAllocationErrors.QuantityNotPositive);
            }

            if (q > AmountMax)
            {
                return Fail(BudgetAllocationErrors.QuantityTooLarge);
            }

            var u = Round(rawUnitCost);
            if (u < 0m)
            {
                return Fail(BudgetAllocationErrors.UnitCostNegative);
            }

            if (u > AmountMax)
            {
                return Fail(BudgetAllocationErrors.UnitCostTooLarge);
            }

            // Any amount the caller sent is ignored: the build-up is the plan. Computed from the
            // rounded factors, so the stored quantity × unit cost always reproduces the amount.
            amount = Round(q * u);
            if (amount > AmountMax)
            {
                return Fail(BudgetAllocationErrors.AmountTooLarge);
            }

            quantity = q;
            unitCost = u;
        }
        else
        {
            if (details.AmountCad is not { } lumpSum)
            {
                return Fail(BudgetAllocationErrors.AmountRequired);
            }

            if (lumpSum < 0m)
            {
                return Fail(BudgetAllocationErrors.AmountNegative);
            }

            if (lumpSum > AmountMax)
            {
                return Fail(BudgetAllocationErrors.AmountTooLarge);
            }

            amount = Round(lumpSum);
        }

        // --- Why ---
        if (string.IsNullOrWhiteSpace(details.Justification))
        {
            return Fail(BudgetAllocationErrors.JustificationRequired);
        }

        var justification = details.Justification.Trim();
        if (justification.Length > JustificationMaxLength)
        {
            return Fail(BudgetAllocationErrors.JustificationTooLong);
        }

        // Enum range checks are not redundant with model binding: JsonStringEnumConverter rejects
        // an unknown *string* with a bare 400, but a numeric 99 binds cleanly (the BudgetCode
        // precedent).
        if (!Enum.IsDefined(details.SpendType))
        {
            return Fail(BudgetAllocationErrors.SpendTypeInvalid);
        }

        if (!Enum.IsDefined(details.Recurrence))
        {
            return Fail(BudgetAllocationErrors.RecurrenceInvalid);
        }

        if (!Enum.IsDefined(details.Priority))
        {
            return Fail(BudgetAllocationErrors.PriorityInvalid);
        }

        var unit = Normalize(details.Unit);
        if (unit?.Length > UnitMaxLength)
        {
            return Fail(BudgetAllocationErrors.UnitTooLong);
        }

        var vendor = Normalize(details.Vendor);
        if (vendor?.Length > VendorMaxLength)
        {
            return Fail(BudgetAllocationErrors.VendorTooLong);
        }

        var tags = new List<string>();
        foreach (var raw in details.Tags ?? [])
        {
            var tag = raw?.Trim() ?? string.Empty;
            if (tag.Length is 0 or > TagMaxLength)
            {
                return Fail(BudgetAllocationErrors.TagInvalid);
            }

            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tag);
            }
        }

        // Counted after de-duplication: "Fuel, fuel" is one tag, not two against the limit.
        if (tags.Count > MaxTags)
        {
            return Fail(BudgetAllocationErrors.TooManyTags);
        }

        var assumptions = Normalize(details.Assumptions);
        if (assumptions?.Length > AssumptionsMaxLength)
        {
            return Fail(BudgetAllocationErrors.AssumptionsTooLong);
        }

        var consequence = Normalize(details.ConsequenceIfUnfunded);
        if (consequence?.Length > ConsequenceMaxLength)
        {
            return Fail(BudgetAllocationErrors.ConsequenceTooLong);
        }

        return Result.Success(new ParsedItem(
            title,
            amount,
            quantity,
            unitCost,
            // A unit describes a quantity; on a lump sum it describes nothing, so it is dropped
            // rather than refused — a console switching from built-up to lump sum may still send it.
            quantity is null ? null : unit,
            justification,
            details.SpendType,
            details.Recurrence,
            vendor,
            tags,
            details.Priority,
            assumptions,
            consequence));

        static Result<ParsedItem> Fail(Error error) => Result.Failure<ParsedItem>(error);
    }

    /// <summary>Blank optional text is stored as null, never as an empty string.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ParsedItem(
        string Title,
        decimal AmountCad,
        decimal? Quantity,
        decimal? UnitCostCad,
        string? Unit,
        string Justification,
        BudgetSpendType SpendType,
        BudgetRecurrence Recurrence,
        string? Vendor,
        IReadOnlyList<string> Tags,
        BudgetItemPriority Priority,
        string? Assumptions,
        string? ConsequenceIfUnfunded);
}
