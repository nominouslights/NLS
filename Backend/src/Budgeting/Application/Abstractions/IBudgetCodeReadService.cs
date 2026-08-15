using NorthernLink.Budgeting.Application.Codes;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_budget_codes. No tenant parameter — the DbContext's tenant query
/// filter (and RLS underneath it) scopes every query to the ambient tenant.
/// </summary>
public interface IBudgetCodeReadService
{
    /// <summary>
    /// The tenant's whole chart of codes, ordered by code. Inactive codes are included:
    /// retiring a code hides it from new allocations, not from the chart it still explains.
    /// </summary>
    Task<IReadOnlyList<BudgetCodeResponse>> GetCodesAsync(CancellationToken cancellationToken = default);
}
