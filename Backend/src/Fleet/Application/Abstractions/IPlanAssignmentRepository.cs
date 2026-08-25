using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the PlanAssignment aggregate (tenant-scoped). One assignment
/// per vehicle, so lookup is by vehicle id.
/// </summary>
public interface IPlanAssignmentRepository
{
    Task<PlanAssignment?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    void Add(PlanAssignment assignment);

    /// <summary>Hard-deletes an assignment. The aggregate must raise its unassignment event first (MarkRemoved).</summary>
    void Remove(PlanAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
