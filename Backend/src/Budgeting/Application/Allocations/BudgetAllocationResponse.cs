namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// The Budgeting module's public representation of one budget item — the shape the period
/// dashboard and the plan screen render directly.
/// <para>
/// <paramref name="Name"/>, <paramref name="Category"/>, <paramref name="ServiceLine"/> and
/// <paramref name="IsCodeActive"/> describe the <em>code</em>, not the item, and are resolved from
/// <c>rm_budget_codes</c> on every read rather than stored on the item: a code's category and name
/// are editable, and an item must follow them. <paramref name="Code"/> is the item's own copy of
/// the code string. The two <c>…Email</c> companions resolve the actor ids through
/// <c>user_lookup</c>, the same way <c>BudgetCodeResponse</c> does. Enums travel as their
/// PascalCase names: <paramref name="SpendType"/> <c>Operating|Capital</c>,
/// <paramref name="Recurrence"/> <c>OneTime|Recurring</c>, <paramref name="Priority"/>
/// <c>MustHave|ShouldHave|NiceToHave</c>.
/// </para>
/// <para>
/// <paramref name="Quantity"/>, <paramref name="UnitCostCad"/> and <paramref name="Unit"/> are
/// null on a lump-sum item; when the first two are set, <paramref name="AmountCad"/> is their
/// product rounded to cents. <paramref name="Justification"/> is empty (never null) on a copied
/// item still waiting to be argued. <paramref name="Tags"/> is never null.
/// </para>
/// <para>
/// <paramref name="Vendor"/> is free text and, on a revenue item, <b>the payer</b> (the field
/// keeps its original name). An expense item's vendors are <paramref name="VendorShares"/> — never
/// null, empty for "No vendor", and when not empty summing to exactly <paramref name="AmountCad"/>.
/// Each share's vendor name and active flag are resolved from <c>rm_vendors</c> at read time.
/// </para>
/// </summary>
public sealed record BudgetAllocationResponse(
    Guid Id,
    Guid PeriodId,
    Guid BudgetCodeId,
    string Code,
    string Name,
    string Category,
    string? ServiceLine,
    bool IsCodeActive,
    string Title,
    decimal AmountCad,
    decimal? Quantity,
    decimal? UnitCostCad,
    string? Unit,
    string Justification,
    string SpendType,
    string Recurrence,
    string? Vendor,
    IReadOnlyList<BudgetItemVendorShareResponse> VendorShares,
    IReadOnlyList<string> Tags,
    string Priority,
    string? Assumptions,
    string? ConsequenceIfUnfunded,
    Guid? CreatedBy,
    string? CreatedByEmail,
    Guid? ModifiedBy,
    string? ModifiedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// One vendor share of a budget item. <paramref name="VendorName"/> is null, and
/// <paramref name="IsActive"/> false, only while the vendor's read row cannot be found — a vendor
/// created a moment ago whose projection is still a poll behind.
/// </summary>
public sealed record BudgetItemVendorShareResponse(
    Guid VendorId,
    string? VendorName,
    bool IsActive,
    decimal AmountCad);
