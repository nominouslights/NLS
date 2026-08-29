using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the plan-assignment write-side repository for handler tests.</summary>
internal sealed class InMemoryPlanAssignmentRepository : IPlanAssignmentRepository
{
    public List<PlanAssignment> Assignments { get; } = [];

    public List<PlanAssignment> Removed { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// Forces the next <see cref="TryAddAsync"/> to report a unique-index conflict —
    /// simulates another request assigning the vehicle between lookup and save.
    /// </summary>
    public bool FailNextTryAdd { get; set; }

    public Task<PlanAssignment?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Assignments.FirstOrDefault(a => a.VehicleId == vehicleId));

    public Task<bool> TryAddAsync(PlanAssignment assignment, CancellationToken cancellationToken = default)
    {
        if (FailNextTryAdd
            || Assignments.Any(a => a.TenantId == assignment.TenantId && a.VehicleId == assignment.VehicleId))
        {
            FailNextTryAdd = false;
            return Task.FromResult(false);
        }

        Assignments.Add(assignment);
        SaveChangesCallCount++;
        return Task.FromResult(true);
    }

    public void Remove(PlanAssignment assignment)
    {
        Assignments.Remove(assignment);
        Removed.Add(assignment);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
