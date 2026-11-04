namespace NorthernLink.Budgeting.Application.Actuals;

/// <summary>
/// One budget code's plan against its spend for a single period — the shape the console's
/// Actuals vs Budget table renders a row from.
/// <para>
/// <b>Both sides of the outer join are included.</b> A code with a plan and no spend yet is a row
/// with <paramref name="ActualCad"/> zero; a code with spend and no plan is a row with
/// <paramref name="PlannedCad"/> zero — and that second one is the whole reason a planner opens
/// this table. Dropping it would hide exactly the thing zero-based budgeting exists to surface:
/// money going somewhere nobody argued for.
/// </para>
/// <para>
/// <paramref name="DeltaCad"/> is <c>Actual − Planned</c>, plainly: positive means more money
/// moved than was planned, whichever side of the ledger the code sits on. <b>No variance band, no
/// percentage, no status.</b> Those thresholds are the console's today, and a server that shipped
/// its own would put two answers to the same question in the product. The console has the two
/// figures and can compute whatever it shows.
/// </para>
/// <para>
/// No tax of any kind appears here or anywhere near it. The platform never computes tax —
/// QuickBooks owns that — so these are the plain amounts as recorded.
/// </para>
/// </summary>
public sealed record BudgetRollupRowResponse(
    Guid BudgetCodeId,
    string Code,
    string Name,
    string Category,
    decimal PlannedCad,
    decimal ActualCad,
    decimal DeltaCad);
