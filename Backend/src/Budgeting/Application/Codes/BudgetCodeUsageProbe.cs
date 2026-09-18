using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// The real answer to "has anything ever been tagged with this code": yes, if any allocation line
/// in any period, <b>or any recorded transaction</b>, carries its id or its string. The actuals
/// half is what the allocation-only probe's own comment said it was waiting for — a code with
/// spend against it is as referenced as one with a plan against it, and hard-deleting it would
/// leave rows on a ledger screen resolving to nothing.
/// <para>
/// Lives in Application rather than Infrastructure because it is pure orchestration over two
/// repositories the tests already fake — which keeps the refusal path unit-testable and keeps the
/// architecture rule (Application never touches EF) trivially true. The allocation side is asked
/// first only because it is the cheaper of the two on a chart of any size; both are indexed
/// seeks, and the short-circuit means an unused code still costs both.
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
    IActualTransactionRepository actuals) : IBudgetCodeUsageProbe
{
    public async Task<bool> IsReferencedAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        await allocations.ExistsForCodeAsync(budgetCodeId, code, cancellationToken)
        || await actuals.ExistsForCodeAsync(budgetCodeId, code, cancellationToken);
}
