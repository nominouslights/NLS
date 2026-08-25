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

    /// <summary>True when the plan exists — the assignment-time referential probe.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another plan (excluding <paramref name="excludePlanId"/>) already uses this
    /// tenant-unique name — the create/rename duplicate check. Tenant scoping comes from the
    /// ambient EF query filter, same as the sibling repositories. The probe is dumb equality:
    /// callers pass the trimmed name, matching what the aggregate stores.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, Guid? excludePlanId = null, CancellationToken cancellationToken = default);

    void Add(MaintenancePlan plan);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
