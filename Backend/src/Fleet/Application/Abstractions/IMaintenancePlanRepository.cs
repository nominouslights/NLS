using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the MaintenancePlan aggregate (tenant-scoped). Deliberately no
/// listing method — listing plans is a read concern and belongs to the read service that
/// arrives with the query handlers, over rm_maintenance_plans.
/// </summary>
public interface IMaintenancePlanRepository
{
    Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks a plan up by its tenant-unique name — the create-time duplicate check and the
    /// seed command's idempotency probe. Tenant scoping comes from the ambient EF query
    /// filter, same as the sibling repositories. The probe compares against the trimmed name,
    /// matching what the aggregate stores.
    /// </summary>
    Task<MaintenancePlan?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    void Add(MaintenancePlan plan);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
