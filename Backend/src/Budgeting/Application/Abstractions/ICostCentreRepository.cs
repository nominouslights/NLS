using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Write-side persistence for the CostCentre aggregate (tenant-scoped through the DbContext's
/// query filter, RLS beneath it). The register is tenant-wide, so no lookup names a period.
/// </summary>
public interface ICostCentreRepository
{
    /// <summary>The tenant's cost centre with that id, or null (including another tenant's id).</summary>
    Task<CostCentre?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's cost centre with that exact (already normalized — trimmed) code, compared
    /// ordinally, or null. Serves the duplicate check and the budget-code validation.
    /// </summary>
    Task<CostCentre?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);

    /// <summary>Whether any cost centre (active or retired) rolls up into this one.</summary>
    Task<bool> HasChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>Whether any <b>active</b> cost centre rolls up into this one — the deactivation guard.</summary>
    Task<bool> HasActiveChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    void Add(CostCentre costCentre);

    /// <summary>Hard delete — the caller runs the children and usage guards first.</summary>
    void Remove(CostCentre costCentre);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
