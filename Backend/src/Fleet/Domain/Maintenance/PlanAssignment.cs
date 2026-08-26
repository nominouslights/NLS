using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// Links one vehicle to the <see cref="MaintenancePlan"/> it follows (one plan per vehicle).
/// Pure linkage — no per-item state lives here: due status derives from
/// <see cref="PmCompletion"/> records, so an item with no completion is simply
/// "not yet recorded".
/// </summary>
public sealed class PlanAssignment : AggregateRoot, ITenantScoped
{
    private PlanAssignment()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid PlanId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }

    public static Result<PlanAssignment> Assign(Guid tenantId, Guid vehicleId, Guid planId)
    {
        if (vehicleId == Guid.Empty)
        {
            return Result.Failure<PlanAssignment>(MaintenanceErrors.VehicleRequired);
        }

        if (planId == Guid.Empty)
        {
            return Result.Failure<PlanAssignment>(MaintenanceErrors.PlanRequired);
        }

        var assignment = new PlanAssignment
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            PlanId = planId,
            AssignedAtUtc = DateTimeOffset.UtcNow,
        };

        assignment.Raise(new PlanAssignedDomainEvent(assignment.Id, vehicleId, planId, tenantId));
        return Result.Success(assignment);
    }

    /// <summary>
    /// Switches the vehicle to a different plan, restarting the assignment clock. Reassigning
    /// the plan it already follows is a no-op — no clock restart, no event.
    /// </summary>
    public Result Reassign(Guid planId)
    {
        if (planId == Guid.Empty)
        {
            return Result.Failure(MaintenanceErrors.PlanRequired);
        }

        if (planId == PlanId)
        {
            return Result.Success();
        }

        PlanId = planId;
        AssignedAtUtc = DateTimeOffset.UtcNow;

        Raise(new PlanAssignedDomainEvent(Id, VehicleId, planId, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Flags this assignment for hard removal. Raised just before the repository deletes the
    /// row so the unassignment lands in the event journal as a real domain event — an
    /// audit/journal record only: no integration-event mapper arm exists for it and none is
    /// planned. Read-model deletion is driven separately by the synthetic aggregate-deleted
    /// journal row.
    /// </summary>
    public void MarkRemoved() => Raise(new PlanUnassignedDomainEvent(Id, VehicleId, TenantId));
}
