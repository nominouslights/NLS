namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>
/// Everything about a budget code that a planner can change after it exists, as one parameter
/// object shared by <see cref="BudgetCode.Create"/> and <see cref="BudgetCode.Update"/> — the
/// <c>ShipmentDetails</c> shape, for the same reason: passing one object makes it structurally
/// impossible for create and edit to drift apart.
/// <para>
/// <b><see cref="BudgetCode.Code"/> is deliberately absent.</b> The code string is the identity a
/// person reads, types, and tags a transaction with; allocations and actuals reference it by
/// string rather than by id. Letting an edit rewrite it would silently orphan every row already
/// tagged, so it is supplied once at creation and never appears here.
/// </para>
/// <para>
/// <b>The actor is deliberately absent too.</b> This record is "what a planner picks"; who is
/// doing the picking is not one of the choices, and it arrives separately from the signed token
/// (see <c>ICurrentActor</c>) precisely so it cannot be supplied alongside the rest of the form.
/// </para>
/// </summary>
public sealed record BudgetCodeDetails
{
    /// <summary>Short human name for what the code covers ("Alamos crew shuttle").</summary>
    public required string Name { get; init; }

    public BudgetCodeCategory Category { get; init; } = BudgetCodeCategory.Expense;

    /// <summary>
    /// How often this code's continued existence should be revisited. Required by the story;
    /// defaulted rather than nullable so a code always carries a review cadence.
    /// </summary>
    public BudgetReviewFrequency ReviewFrequency { get; init; } = BudgetReviewFrequency.Quarterly;

    /// <summary>The line of business this code rolls up into, for revenue-mix reporting.</summary>
    public BudgetServiceLine? ServiceLine { get; init; }

    /// <summary>
    /// Internal cost centre. Degenerate for a sole proprietorship — Northern Link has exactly
    /// one — and carried now because an NLBC tenant with departments will need it.
    /// </summary>
    public string? CostCentre { get; init; }

    /// <summary>
    /// Optional parent for one-level rollup reporting. Validated in the application layer
    /// (<c>BudgetCodeParentRule</c>), not here: the aggregate cannot see the tenant's other codes.
    /// </summary>
    public Guid? ParentCodeId { get; init; }

    /// <summary>
    /// The matching account in the QuickBooks chart of accounts. Free text — see
    /// <see cref="BudgetCode"/> for why nothing validates it.
    /// </summary>
    public string? GlAccountCode { get; init; }

    public BudgetTaxTreatment? TaxTreatment { get; init; }

    /// <summary>
    /// The accountable person, as a user id validated against Budgeting's <c>user_lookup</c>
    /// replica. A real reference rather than the free-text name the codebase uses elsewhere,
    /// because "who answers for this budget" is a question an audit asks.
    /// </summary>
    public Guid? BudgetOwnerUserId { get; init; }

    /// <summary>
    /// What this code covers, and what it doesn't. Optional, and deliberately <em>not</em> the
    /// zero-based justification this field replaced: architecture §5.3 says codes are re-justified
    /// <em>each period</em>, so the recurring justification belongs on the allocation (Stage 6.2),
    /// which is where the per-period decision actually happens. What stays on the code is the
    /// standing note — the part that does not change when the calendar does.
    /// </summary>
    public string? Description { get; init; }
}
