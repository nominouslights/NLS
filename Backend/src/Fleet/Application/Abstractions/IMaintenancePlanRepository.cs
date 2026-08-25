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
    /// Like <see cref="GetByIdAsync"/> but untracked — for cross-aggregate probes (e.g. the
    /// completion handler's code-membership check) that read a plan document without ever
    /// saving it, so its 260-line jsonb payload never enters the change tracker.
    /// </summary>
    Task<MaintenancePlan?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the plan exists — the assignment-time referential probe.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The id of the plan with this tenant-unique name, or null when none exists — the one
    /// name lookup every duplicate probe and the seed command share (existence is
    /// <c>is not null</c>; rename checks compare the id against their own plan). Tenant
    /// scoping comes from the ambient EF query filter, same as the sibling repositories.
    /// Dumb equality: callers pass the trimmed name, matching what the aggregate stores.
    /// </summary>
    Task<Guid?> FindIdByNameAsync(string name, CancellationToken cancellationToken = default);

    void Add(MaintenancePlan plan);

    /// <summary>
    /// Adds a NEW plan and saves in one step, reporting false when the tenant-unique name
    /// was taken concurrently (the unique (tenant_id, name) index fired) instead of letting
    /// the constraint violation escape as a 500 — the idempotent seed's check-then-insert
    /// closer (the PlanAssignment TryAdd pattern).
    /// </summary>
    Task<bool> TryAddAsync(MaintenancePlan plan, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
