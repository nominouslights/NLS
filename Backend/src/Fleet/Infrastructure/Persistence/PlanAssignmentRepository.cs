using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class PlanAssignmentRepository(FleetDbContext context) : IPlanAssignmentRepository
{
    public Task<PlanAssignment?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.PlanAssignments.FirstOrDefaultAsync(a => a.VehicleId == vehicleId, cancellationToken);

    public void Add(PlanAssignment assignment) => context.PlanAssignments.Add(assignment);

    public void Remove(PlanAssignment assignment) => context.PlanAssignments.Remove(assignment);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
