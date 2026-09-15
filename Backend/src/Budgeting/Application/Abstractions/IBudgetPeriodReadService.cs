using NorthernLink.Budgeting.Application.Periods;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_budget_periods. No tenant parameter — the DbContext's
/// tenant query filter (and RLS underneath it) scopes every query to the ambient tenant.
/// Both reads carry the period's planned totals, summed from its allocation lines.
/// </summary>
public interface IBudgetPeriodReadService
{
    Task<IReadOnlyList<BudgetPeriodResponse>> GetPeriodsAsync(CancellationToken cancellationToken = default);

    /// <summary>One period by id, or null when the tenant has no such period.</summary>
    Task<BudgetPeriodResponse?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);
}
