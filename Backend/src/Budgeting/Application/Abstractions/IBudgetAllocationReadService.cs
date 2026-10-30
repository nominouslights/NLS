using NorthernLink.Budgeting.Application.Allocations;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_budget_allocations. No tenant parameter — the DbContext's tenant
/// query filter (and RLS underneath it) scopes every query to the ambient tenant.
/// </summary>
public interface IBudgetAllocationReadService
{
    /// <summary>
    /// Every budget item of one period, ordered by code, then priority (MustHave first), then
    /// creation time, with the code's current name, category,
    /// service line and active flag resolved from <c>rm_budget_codes</c> at read time.
    /// </summary>
    Task<IReadOnlyList<BudgetAllocationResponse>> GetForPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);
}
