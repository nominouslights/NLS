namespace NorthernLink.Budgeting.Application.Actuals;

/// <summary>
/// The Budgeting module's public representation of one recorded transaction — the shape the
/// period view's actuals list renders directly.
/// <para>
/// <paramref name="Name"/>, <paramref name="Category"/>, <paramref name="ServiceLine"/> and
/// <paramref name="IsCodeActive"/> describe the <em>code</em>, not the transaction, and are
/// resolved from <c>rm_budget_codes</c> on every read rather than stored on the row: a code's
/// category and name are editable, and a figure must follow them.
/// <paramref name="Code"/> is the transaction's own snapshot of the code string. The two
/// <c>…Email</c> companions resolve the actor ids through <c>user_lookup</c>, the same way
/// <c>BudgetAllocationResponse</c> does. Enums travel as their PascalCase names.
/// </para>
/// <para>
/// <b>There is no period id on the wire</b>, and that is not an omission — the period is derived
/// from <paramref name="OccurredOn"/> and never stored. The console asked for a period's actuals
/// by period; every row it gets back is inside that period's dates by construction.
/// </para>
/// </summary>
public sealed record ActualTransactionResponse(
    Guid Id,
    Guid BudgetCodeId,
    string Code,
    string Name,
    string Category,
    string? ServiceLine,
    bool IsCodeActive,
    decimal AmountCad,
    DateOnly OccurredOn,
    string Description,
    string Source,
    string? ExternalReference,
    Guid? CreatedBy,
    string? CreatedByEmail,
    Guid? ModifiedBy,
    string? ModifiedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
