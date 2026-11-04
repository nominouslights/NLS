using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// The real answer to "has anything ever been tagged with this code": yes, if any budget item in
/// the code's own period, <b>or any recorded transaction dated inside that period</b>, carries its
/// id or its string. The actuals half is what the allocation-only probe's own comment said it was
/// waiting for — a code with spend against it is as referenced as one with a plan against it, and
/// hard-deleting it would leave rows on a ledger screen resolving to nothing.
/// <para>
/// <b>Period-scoped on both halves, because codes are</b> — see <see cref="IBudgetCodeUsageProbe"/>.
/// An item names its period directly. A transaction names only its date, so the period's own
/// dates bound the actuals half: Q3's spend on its FUEL must not pin Q4's unused FUEL any more
/// than Q3's items may. An unknown period id has no dates and so no actuals to find; the
/// allocation half still answers, which keeps the probe total rather than throwing.
/// </para>
/// <para>
/// Lives in Application rather than Infrastructure because it is pure orchestration over
/// repositories the tests already fake — which keeps the refusal path unit-testable and keeps the
/// architecture rule (Application never touches EF) trivially true. The allocation side is asked
/// first only because it is the cheaper of the two on a chart of any size.
/// </para>
/// <para>
/// The free-text <c>budget_code</c> strings on <c>clients.contracts</c>,
/// <c>clients.purchase_orders</c> and <c>fleet.work_orders</c> are still <em>not</em> consulted:
/// those are strings a dispatcher typed, not references to these rows, and treating them as such
/// would refuse deletes on a coincidental match. They also belong to other domain libraries,
/// which this one may not reference.
/// </para>
/// </summary>
public sealed class BudgetCodeUsageProbe(
    IBudgetAllocationRepository allocations,
    IActualTransactionRepository actuals,
    IBudgetPeriodRepository periods) : IBudgetCodeUsageProbe
{
    public async Task<bool> IsReferencedAsync(
        Guid periodId, Guid budgetCodeId, string code, CancellationToken cancellationToken = default)
    {
        if (await allocations.ExistsForCodeAsync(periodId, budgetCodeId, code, cancellationToken))
        {
            return true;
        }

        var period = await periods.GetByIdAsync(periodId, cancellationToken);
        return period is not null
            && await actuals.ExistsForCodeAsync(budgetCodeId, code, period.StartsOn, period.EndsOn, cancellationToken);
    }
}
