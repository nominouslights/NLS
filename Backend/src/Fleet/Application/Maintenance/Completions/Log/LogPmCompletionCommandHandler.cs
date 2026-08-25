using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Completions.Log;

public sealed class LogPmCompletionCommandHandler(
    IPmCompletionRepository completions,
    IPlanAssignmentRepository assignments,
    IMaintenancePlanRepository plans,
    IVehicleRepository vehicles,
    IWorkOrderRepository workOrders)
    : ICommandHandler<LogPmCompletionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(LogPmCompletionCommand command, CancellationToken cancellationToken)
    {
        var assignment = await assignments.GetByVehicleIdAsync(command.VehicleId, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure<Guid>(MaintenanceErrors.AssignmentNotFound);
        }

        // The domain owns validation and normalization (blank/length/kind/trim) — run it
        // first so the cross-aggregate checks below operate on the normalized completion.
        var completionResult = PmCompletion.Log(
            command.TenantId,
            command.VehicleId,
            assignment.PlanId,
            command.Code,
            command.Kind,
            command.PerformedAt,
            command.OdometerKm,
            command.PerformedBy,
            command.WorkOrderId,
            command.Measurement,
            command.Notes);

        if (completionResult.IsFailure)
        {
            return Result.Failure<Guid>(completionResult.Error);
        }

        var completion = completionResult.Value;

        var vehicle = await vehicles.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure<Guid>(VehicleErrors.NotFound);
        }

        // A sold/recycled unit is out of the fleet — its PM ledger is closed.
        if (vehicle.IsDisposed)
        {
            return Result.Failure<Guid>(VehicleErrors.Disposed);
        }

        // Plausibility: readings below the vehicle's current odometer are legal (historical
        // entries), but far ahead of it means a typo — reject before it poisons due math.
        if (completion.OdometerKm > vehicle.OdometerKm + PmCompletion.MaxOdometerAheadKm)
        {
            return Result.Failure<Guid>(MaintenanceErrors.OdometerImplausible(vehicle.OdometerKm));
        }

        var plan = await plans.GetByIdAsync(assignment.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<Guid>(MaintenanceErrors.PlanNotFound);
        }

        // The code must exist in the assigned plan for the stated kind — item codes and
        // overhaul codes are separate namespaces (PM-… vs OH-…), so no cross-kind match.
        var codeExists = completion.Kind == PmEntryKind.Item
            ? plan.Items.Any(i => string.Equals(i.Code, completion.ItemCode, StringComparison.Ordinal))
            : plan.Overhauls.Any(o => string.Equals(o.Code, completion.ItemCode, StringComparison.Ordinal));

        if (!codeExists)
        {
            return Result.Failure<Guid>(MaintenanceErrors.CompletionCodeNotInPlan);
        }

        // The work-order link is optional, but a given id must be real.
        if (command.WorkOrderId is { } workOrderId
            && !await workOrders.ExistsAsync(workOrderId, cancellationToken))
        {
            return Result.Failure<Guid>(WorkOrderErrors.NotFound);
        }

        completions.Add(completion);
        await completions.SaveChangesAsync(cancellationToken);
        return Result.Success(completion.Id);
    }
}
