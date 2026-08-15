using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Reports that no budget code has ever been referenced — which is not a stub, it is the true
/// answer today. Nothing in the database points at a budget code yet:
/// <list type="bullet">
/// <item><c>budget_allocations</c> and <c>actual_transactions</c> do not exist (Stage 6.2).</item>
/// <item>
/// The free-text <c>budget_code</c> strings on <c>clients.contracts</c>,
/// <c>clients.purchase_orders</c> and <c>fleet.work_orders</c> are unrelated strings a dispatcher
/// typed, not references to these rows. Validating one against the other is its own story, listed
/// out of scope in <c>Budgeting/CLAUDE.md</c> — so treating them as references here would refuse
/// deletes on the strength of a coincidental string match.
/// </item>
/// </list>
/// <para>
/// Stage 6.2 replaces this class with one that queries the allocation and actual-transaction
/// tables by both id and code string. Nothing else changes: the interface, the handler and the
/// error already exist, and <c>DeleteBudgetCodeCommandHandlerTests</c> already pins the refusal
/// path against a stub that reports true.
/// </para>
/// </summary>
internal sealed class NeverReferencedBudgetCodeUsageProbe : IBudgetCodeUsageProbe
{
    public Task<bool> IsReferencedAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
