namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// The Budgeting module's public representation of one allocation line — the shape the period
/// dashboard and the Allocations screen render directly.
/// <para>
/// <paramref name="Name"/>, <paramref name="Category"/>, <paramref name="ServiceLine"/> and
/// <paramref name="IsCodeActive"/> describe the <em>code</em>, not the line, and are resolved from
/// <c>rm_budget_codes</c> on every read rather than stored on the line: a code's category and name
/// are editable, and a line must follow them. <paramref name="Code"/> is the line's own immutable
/// copy of the code string. The two <c>…Email</c> companions resolve the actor ids through
/// <c>user_lookup</c>, the same way <c>BudgetCodeResponse</c> does. Enums travel as their
/// PascalCase names.
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
    decimal AmountCad,
    string Justification,
    Guid? CreatedBy,
    string? CreatedByEmail,
    Guid? ModifiedBy,
    string? ModifiedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
