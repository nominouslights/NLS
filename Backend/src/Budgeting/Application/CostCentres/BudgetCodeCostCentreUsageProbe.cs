using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.CostCentres;

/// <summary>
/// The real answer to "does anything carry this cost centre": yes, if any budget code in any
/// period does (retired codes and closed periods included — a closed period's chart still
/// explains its numbers). In Application rather than Infrastructure for the
/// <c>AllocationBudgetCodeUsageProbe</c> reason: pure orchestration over a repository the tests
/// already fake. Actual transactions, when they arrive, are one more <c>||</c> here.
/// </summary>
public sealed class BudgetCodeCostCentreUsageProbe(IBudgetCodeRepository codes) : ICostCentreUsageProbe
{
    public Task<bool> IsReferencedAsync(string costCentreCode, CancellationToken cancellationToken = default) =>
        codes.AnyWithCostCentreAsync(costCentreCode, cancellationToken);
}
