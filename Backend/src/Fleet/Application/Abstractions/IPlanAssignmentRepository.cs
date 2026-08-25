using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the PlanAssignment aggregate (tenant-scoped). One assignment
/// per vehicle, so lookup is by vehicle id.
/// </summary>
public interface IPlanAssignmentRepository
{
    Task<PlanAssignment?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a NEW assignment and saves in one step, reporting false when the vehicle gained
    /// an assignment concurrently (the unique (tenant_id, vehicle_id) index fired) instead
    /// of letting the constraint violation escape as an exception.
    /// </summary>
    Task<bool> TryAddAsync(PlanAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes an assignment. The aggregate must raise its unassignment event first (MarkRemoved).</summary>
    void Remove(PlanAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
