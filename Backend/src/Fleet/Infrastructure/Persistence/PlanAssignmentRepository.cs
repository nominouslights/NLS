using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class PlanAssignmentRepository(FleetDbContext context) : IPlanAssignmentRepository
{
    public Task<PlanAssignment?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.PlanAssignments.FirstOrDefaultAsync(a => a.VehicleId == vehicleId, cancellationToken);

    public async Task<bool> TryAddAsync(PlanAssignment assignment, CancellationToken cancellationToken = default)
    {
        context.PlanAssignments.Add(assignment);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_pm_plan_assignments_tenant_id_vehicle_id",
        })
        {
            // Someone assigned this vehicle between the handler's lookup and this commit.
            // The single failed SaveChanges persisted nothing; detach the failed entries so
            // the context stays usable and report the race as a domain condition, not a 500
            // (the UserRepository.TryAddNewUserAsync pattern).
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public void Remove(PlanAssignment assignment) => context.PlanAssignments.Remove(assignment);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
