using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Allocations.Events;
using NorthernLink.Budgeting.Domain.Codes;

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
/// <para>
/// <b>Vendors: shares on expense items, a payer on revenue items.</b> An expense item names its
/// vendors as <see cref="VendorShares"/> — each a register vendor with a dollar share, summing to
/// exactly <see cref="AmountCad"/> when there are any (none at all is "No vendor"; a partial split
/// is refused, and so is an amount change the shares no longer add up to — never rebalanced). A
/// revenue item has no shares; its free-text <see cref="Vendor"/> is the payer. Which kind an item
/// is depends on its code's category, which the caller passes in (the aggregate holds only the
/// code's id and string). Whether a share's vendor exists and is active is the handlers' check —
/// the register is another aggregate.
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

    /// <summary>How many vendors one item can be split across — far beyond any real split, and a bound on the request.</summary>
    public const int MaxVendorShares = 20;

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

    /// <summary>
    /// Free text. On a revenue item it is the <b>payer</b>; an expense item names its vendors
    /// through <see cref="VendorShares"/>, and refuses new free text here (an expense item that
    /// already carried text — a $0 legacy item the vendor-shares backfill could not split — keeps
    /// it until somebody changes it). The column keeps its original name, <c>vendor</c>.
    /// </summary>
    public string? Vendor { get; private set; }

    /// <summary>
    /// An expense item's vendors with their dollar shares, in the order the planner gave them.
    /// Empty means "No vendor". When not empty, the amounts sum to exactly <see cref="AmountCad"/>
    /// and each vendor appears once.
    /// </summary>
    public List<BudgetItemVendorShare> VendorShares { get; private set; } = [];

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
    /// enums, unit, vendor, vendor shares (each share, duplicates, then the sum against the
    /// amount), tags, assumptions, consequence. The category rules (no shares on revenue, no new
    /// free text on expense) need the code, so they run in <see cref="Create"/>/<see cref="Update"/>.
    /// Null shares are not checked here — on an update they mean "keep the current ones", which
    /// <see cref="Update"/> re-checks against the new amount.
    /// </summary>
    public static Result Validate(BudgetItemDetails details)
    {
        var parsed = Parse(details, []);
        return parsed.IsSuccess ? Result.Success() : Result.Failure(parsed.Error);
    }

    /// <summary>
    /// Creates an item. The code string is copied here; <paramref name="codeCategory"/> is the
    /// code's current category, which decides whether the item may carry vendor shares (expense)
    /// or a free-text payer (revenue).
    /// </summary>
    public static Result<BudgetAllocation> Create(
        Guid tenantId,
        Guid periodId,
        Guid budgetCodeId,
        string code,
        BudgetCodeCategory codeCategory,
        BudgetItemDetails details,
        Guid? actorId)
    {
        var parsed = Parse(details, []);
        if (parsed.IsFailure)
        {
            return Result.Failure<BudgetAllocation>(parsed.Error);
        }

        var categoryRule = CheckCategory(codeCategory, parsed.Value, sharesChanged: true, payerChanged: true);
        if (categoryRule.IsFailure)
        {
            return Result.Failure<BudgetAllocation>(categoryRule.Error);
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
    /// the argument</b>: same tenant, same budget code <em>string</em> — re-pointed at
    /// <paramref name="budgetCodeId"/>, the target period's code with that string, because codes
    /// belong to a period and the source's code id names a code of the source period — same title, amount, cost
    /// build-up, classification, vendor shares or payer (see below), tags, priority, assumptions
    /// and consequence — and <see cref="Justification"/> set to <see cref="string.Empty"/>.
    /// <para>
    /// <b>Vendors follow the target code's category</b> (<paramref name="targetCategory"/>), because
    /// the same code string can be classified differently in another period: onto an expense code
    /// the <see cref="VendorShares"/> come across as-is (vendors are tenant-wide, and the amount is
    /// unchanged, so they still sum) — as <em>new</em> share instances, because EF tracks owned
    /// entities by reference — and onto a revenue code the shares are dropped. A share on a
    /// since-retired vendor is copied too — it is the same planning decision carried forward, not
    /// a new pick of that vendor.
    /// </para>
    /// <para>
    /// <b>The free text comes across whenever the shares do not</b>: always onto a revenue code
    /// (it is the payer), and onto an expense code when the source item has no shares — the $0
    /// legacy expense item the vendor-shares backfill deliberately left holding its text, or a
    /// revenue item whose code string is an expense code in the target. Dropping it would silently
    /// lose the only record of that vendor. This is not the planner adding free text to an expense
    /// item, so <c>PayerNotAllowedForExpense</c> does not apply (that rule is "only on change", and
    /// a copy changes nothing); the copied item saves as it stands, and the first edit that changes
    /// the text meets the rule. Onto an expense code from an item that <em>has</em> shares, any
    /// leftover text is dropped: the shares are that item's vendor record and supersede it.
    /// </para>
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
    /// mismatched tenant or code string — they come from the item being copied; only the period,
    /// the target period's code id and the actor are supplied. The caller (the copy handler) is
    /// what guarantees that code carries this item's string: it resolves the id by string. Nothing here can fail: every field was already
    /// validated and rounded when the source item was written. Raises
    /// <see cref="BudgetAllocationCreatedDomainEvent"/> exactly as <see cref="Create"/> does.
    /// </para>
    /// </summary>
    public BudgetAllocation CopyInto(Guid periodId, Guid budgetCodeId, BudgetCodeCategory targetCategory, Guid? actorId)
    {
        var isRevenue = targetCategory == BudgetCodeCategory.Revenue;
        var now = DateTimeOffset.UtcNow;
        var copy = new BudgetAllocation
        {
            TenantId = TenantId,
            PeriodId = periodId,
            BudgetCodeId = budgetCodeId,
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
            // Text crosses whenever no shares do (see the doc comment): dropping it would lose the
            // only record of the vendor on a $0 legacy item the backfill could not split.
            Vendor = isRevenue || VendorShares.Count == 0 ? Vendor : null,
            // New instances, never the source's: EF tracks owned JSON entities by reference, and
            // the source item is tracked in the same unit of work — sharing them would hand one
            // dependent two owners and make the save throw.
            VendorShares = isRevenue ? [] : [.. VendorShares.Select(share => share.CopyForNewOwner())],
            Tags = [.. Tags],
            Priority = Priority,
            Assumptions = Assumptions,
            ConsequenceIfUnfunded = ConsequenceIfUnfunded,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        copy.Raise(new BudgetAllocationCreatedDomainEvent(
            copy.Id, TenantId, periodId, budgetCodeId, Code, Title, AmountCad, actorId));
        return copy;
    }

    /// <summary>
    /// Rewrites the item, including (optionally) moving it to another budget code — the handler
    /// has already checked that code exists and is active. The period never changes. Always raises
    /// the updated event, even when nothing changed: a re-submitted item is still a decision the
    /// journal should carry, and an eventless Modified save is refused by the audit pipeline anyway.
    /// A refused update changes nothing and raises nothing.
    /// <para>
    /// <b>Vendor shares.</b> Null shares in <paramref name="details"/> keep the current ones, and
    /// they are re-checked against the new amount like any others — so changing the amount of an
    /// item with shares, without restating them, is refused with
    /// <c>VendorSharesMustSumToAmount</c> rather than rebalanced. The category rules apply <b>only
    /// on change</b> (the cost-centre leniency): an item whose code was re-classified since, or a
    /// $0 legacy expense item still carrying its free-text vendor, can be saved as it stands; a
    /// write that changes the shares, the payer text or the code meets the rule.
    /// </para>
    /// </summary>
    public Result Update(
        Guid budgetCodeId, string code, BudgetCodeCategory codeCategory, BudgetItemDetails details, Guid? actorId)
    {
        var parsed = Parse(details, VendorShares);
        if (parsed.IsFailure)
        {
            return Result.Failure(parsed.Error);
        }

        var codeChanged = budgetCodeId != BudgetCodeId;
        var categoryRule = CheckCategory(
            codeCategory,
            parsed.Value,
            sharesChanged: codeChanged || !SameShares(parsed.Value.VendorShares, VendorShares),
            payerChanged: codeChanged || !string.Equals(parsed.Value.Vendor, Vendor, StringComparison.Ordinal));
        if (categoryRule.IsFailure)
        {
            return categoryRule;
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
        VendorShares = [.. item.VendorShares];
        Tags = [.. item.Tags];
        Priority = item.Priority;
        Assumptions = item.Assumptions;
        ConsequenceIfUnfunded = item.ConsequenceIfUnfunded;
    }

    /// <summary>
    /// The vendor ids <paramref name="details"/> adds to an item that currently has
    /// <paramref name="current"/> shares — the vendors a handler must find, active, in the register.
    /// Empty when the shares are not stated (null keeps the current ones) and when every named
    /// vendor already has a share here (a retained share on a since-retired vendor stays valid).
    /// Distinct, in request order; a missing or empty id is skipped (the aggregate reports it).
    /// </summary>
    public static IReadOnlyList<Guid> AddedVendorIds(
        BudgetItemDetails details, IReadOnlyCollection<BudgetItemVendorShare> current)
    {
        if (details.VendorShares is null)
        {
            return [];
        }

        var existing = current.Select(share => share.VendorId).ToHashSet();
        return details.VendorShares
            .Select(share => share?.VendorId)
            .OfType<Guid>()
            .Where(id => id != Guid.Empty && !existing.Contains(id))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The category rules, applied only to what the write changes: no vendor shares on a revenue
    /// item, no new free-text payer on an expense item.
    /// </summary>
    private static Result CheckCategory(
        BudgetCodeCategory category, ParsedItem item, bool sharesChanged, bool payerChanged)
    {
        if (category == BudgetCodeCategory.Revenue)
        {
            return item.VendorShares.Count > 0 && sharesChanged
                ? Result.Failure(BudgetAllocationErrors.VendorSharesNotAllowedForRevenue)
                : Result.Success();
        }

        return item.Vendor is not null && payerChanged
            ? Result.Failure(BudgetAllocationErrors.PayerNotAllowedForExpense)
            : Result.Success();
    }

    /// <summary>Same vendors with the same amounts, in any order (each vendor appears once).</summary>
    private static bool SameShares(
        IReadOnlyCollection<BudgetItemVendorShare> left, IReadOnlyCollection<BudgetItemVendorShare> right) =>
        left.Count == right.Count && left.All(right.Contains);

    /// <summary>
    /// Validates and normalizes in one pass, so what is checked is exactly what is stored.
    /// <paramref name="currentShares"/> stands in when the details state none (null).
    /// </summary>
    private static Result<ParsedItem> Parse(BudgetItemDetails details, IReadOnlyList<BudgetItemVendorShare> currentShares)
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

        // --- Vendor shares: each one, then duplicates, then the sum against the (computed) amount ---
        var shares = new List<BudgetItemVendorShare>();
        if (details.VendorShares is null)
        {
            shares.AddRange(currentShares);
        }
        else
        {
            if (details.VendorShares.Count > MaxVendorShares)
            {
                return Fail(BudgetAllocationErrors.TooManyVendorShares);
            }

            foreach (var raw in details.VendorShares)
            {
                var share = BudgetItemVendorShare.Create(raw?.VendorId, raw?.AmountCad);
                if (share.IsFailure)
                {
                    return Fail(share.Error);
                }

                if (shares.Exists(existing => existing.VendorId == share.Value.VendorId))
                {
                    return Fail(BudgetAllocationErrors.DuplicateVendorShare);
                }

                shares.Add(share.Value);
            }
        }

        // No partial splits: any share at all means the shares are the whole amount, to the cent.
        // Exact decimal arithmetic on 2-dp values, bounded by MaxVendorShares × AmountMax, so the
        // sum can neither round nor overflow. A $0 item therefore cannot carry a share (every
        // share is positive) — it is "No vendor" by construction.
        if (shares.Count > 0)
        {
            var sharesTotal = shares.Sum(share => share.AmountCad);
            if (sharesTotal != amount)
            {
                return Fail(BudgetAllocationErrors.VendorSharesMustSumToAmount(sharesTotal, amount));
            }
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
            shares,
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
        IReadOnlyList<BudgetItemVendorShare> VendorShares,
        IReadOnlyList<string> Tags,
        BudgetItemPriority Priority,
        string? Assumptions,
        string? ConsequenceIfUnfunded);
}
