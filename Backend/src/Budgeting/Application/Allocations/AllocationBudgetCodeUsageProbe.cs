using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// The real answer to "has anything ever been tagged with this code": yes, if any allocation
/// line in any period carries its id or its string. Replaces the never-referenced probe that
/// held the slot while nothing could reference a code.
/// <para>
/// Lives in Application rather than Infrastructure because it is pure orchestration over a
/// repository the tests already fake — which keeps the refusal path unit-testable and keeps the
/// architecture rule (Application never touches EF) trivially true. The free-text
/// <c>budget_code</c> strings on <c>clients.contracts</c>, <c>clients.purchase_orders</c> and
/// <c>fleet.work_orders</c> are still <em>not</em> consulted: those are strings a dispatcher
/// typed, not references to these rows, and treating them as such would refuse deletes on a
/// coincidental match. Actual transactions, when they arrive, are one more <c>||</c> here.
/// </para>
/// </summary>
public sealed class AllocationBudgetCodeUsageProbe(IBudgetAllocationRepository allocations) : IBudgetCodeUsageProbe
{
    public Task<bool> IsReferencedAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        allocations.ExistsForCodeAsync(budgetCodeId, code, cancellationToken);
}
