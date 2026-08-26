using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Assignments.Assign;

public sealed class AssignMaintenancePlanCommandHandler(
    IPlanAssignmentRepository assignments,
    IMaintenancePlanRepository plans,
    IVehicleRepository vehicles)
    : ICommandHandler<AssignMaintenancePlanCommand>
{
    public async Task<Result> Handle(AssignMaintenancePlanCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(VehicleErrors.NotFound);
        }

        // A sold/recycled unit is out of the fleet — nothing left to maintain.
        if (vehicle.IsDisposed)
        {
            return Result.Failure(VehicleErrors.Disposed);
        }

        if (!await plans.ExistsAsync(command.PlanId, cancellationToken))
        {
            return Result.Failure(MaintenanceErrors.PlanNotFound);
        }

        var existing = await assignments.GetByVehicleIdAsync(command.VehicleId, cancellationToken);
        if (existing is not null)
        {
            var reassignResult = existing.Reassign(command.PlanId);
            if (reassignResult.IsFailure)
            {
                return reassignResult;
            }

            await assignments.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var assignmentResult = PlanAssignment.Assign(command.TenantId, command.VehicleId, command.PlanId);
        if (assignmentResult.IsFailure)
        {
            return Result.Failure(assignmentResult.Error);
        }

        // TryAdd absorbs the create/create race: if another request assigned this vehicle
        // between the lookup above and this save, the unique (tenant_id, vehicle_id) index
        // fires and we answer with a domain conflict instead of a 500.
        if (!await assignments.TryAddAsync(assignmentResult.Value, cancellationToken))
        {
            return Result.Failure(MaintenanceErrors.AssignmentConflict);
        }

        return Result.Success();
    }
}
