using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Assignments.Unassign;

public sealed class UnassignMaintenancePlanCommandHandler(IPlanAssignmentRepository assignments)
    : ICommandHandler<UnassignMaintenancePlanCommand>
{
    public async Task<Result> Handle(UnassignMaintenancePlanCommand command, CancellationToken cancellationToken)
    {
        var assignment = await assignments.GetByVehicleIdAsync(command.VehicleId, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure(MaintenanceErrors.AssignmentNotFound);
        }

        // Raise the unassignment event before the hard delete so it lands in the event
        // journal (the inspection-remove pattern).
        assignment.MarkRemoved();
        assignments.Remove(assignment);
        await assignments.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
