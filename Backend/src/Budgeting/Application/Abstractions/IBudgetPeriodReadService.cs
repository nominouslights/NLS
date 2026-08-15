using NorthernLink.Budgeting.Application.Periods;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_budget_periods. No tenant parameter — the DbContext's
/// tenant query filter (and RLS underneath it) scopes every query to the ambient tenant.
/// </summary>
public interface IBudgetPeriodReadService
{
    Task<IReadOnlyList<BudgetPeriodResponse>> GetPeriodsAsync(CancellationToken cancellationToken = default);
}
