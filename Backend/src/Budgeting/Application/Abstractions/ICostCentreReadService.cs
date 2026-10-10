using NorthernLink.Budgeting.Application.CostCentres;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_cost_centres (and, for the rollup, rm_budget_codes and
/// rm_budget_allocations). No tenant parameter — the DbContext's tenant query filter (and RLS
/// underneath it) scopes every query to the ambient tenant.
/// </summary>
public interface ICostCentreReadService
{
    /// <summary>The register ordered by code; retired entries only when <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<CostCentreResponse>> GetCostCentresAsync(
        bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>One entry by id, retired or not, or null.</summary>
    Task<CostCentreResponse?> GetCostCentreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One period's planned expense per cost centre (see <see cref="CostCentrePlannedRollup"/>).
    /// The caller has already proved the period exists.
    /// </summary>
    Task<CostCentreRollupResponse> GetPlannedRollupAsync(
        Guid periodId, CancellationToken cancellationToken = default);
}
