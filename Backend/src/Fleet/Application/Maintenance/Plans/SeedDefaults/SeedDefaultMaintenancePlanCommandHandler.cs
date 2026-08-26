using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;

public sealed class SeedDefaultMaintenancePlanCommandHandler(
    IMaintenancePlanRepository plans,
    IPlanAssignmentRepository assignments,
    IVehicleRepository vehicles)
    : ICommandHandler<SeedDefaultMaintenancePlanCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SeedDefaultMaintenancePlanCommand command, CancellationToken cancellationToken)
    {
        // 1. The plan, by its tenant-unique name. Existing → reuse the id untouched (no
        //    recreate, no update — operators may have tuned the intervals, or hand-created a
        //    plan under this name before ever seeding); missing → create from the
        //    transcribed seed data, with TryAdd closing the check-then-insert race: if the
        //    unique (tenant_id, name) index fires, a concurrent request seeded the plan
        //    first, and re-probing yields its id — the same 200 + existing-id outcome.
        var planId = await plans.FindIdByNameAsync(TransitSeverePlanSeed.PlanName, cancellationToken);
        if (planId is null)
        {
            var planResult = MaintenancePlan.Create(
                command.TenantId,
                TransitSeverePlanSeed.PlanName,
                TransitSeverePlanSeed.VehicleModel,
                TransitSeverePlanSeed.ServiceClass,
                notes: null,
                TransitSeverePlanSeed.BuildItems(),
                TransitSeverePlanSeed.BuildOverhauls());

            if (planResult.IsFailure)
            {
                return Result.Failure<Guid>(planResult.Error);
            }

            planId = await plans.TryAddAsync(planResult.Value, cancellationToken)
                ? planResult.Value.Id
                : await plans.FindIdByNameAsync(TransitSeverePlanSeed.PlanName, cancellationToken);

            if (planId is null)
            {
                // TryAdd lost the race yet the winner's row vanished before the re-probe
                // (concurrent seed + delete). Vanishingly unlikely; a rerun will seed it.
                return Result.Failure<Guid>(MaintenanceErrors.PlanNotFound);
            }
        }

        // 2. The unit the plan is meant for: VIN first, unit number as fallback. A missing
        //    or disposed vehicle is not a failure — the plan is seeded either way, and the
        //    assignment can be made by hand once the unit is registered.
        var vehicle = await FindSeedVehicleAsync(cancellationToken);
        if (vehicle is null || vehicle.IsDisposed)
        {
            return Result.Success(planId.Value);
        }

        // 3. Assign only a vehicle that follows no plan yet — an existing assignment (to
        //    any plan) is an operator decision this seed never overrides.
        var existing = await assignments.GetByVehicleIdAsync(vehicle.Id, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(planId.Value);
        }

        var assignmentResult = PlanAssignment.Assign(command.TenantId, vehicle.Id, planId.Value);
        if (assignmentResult.IsFailure)
        {
            return Result.Failure<Guid>(assignmentResult.Error);
        }

        // TryAdd absorbs the concurrent-assignment race: if the unique (tenant_id,
        // vehicle_id) index fires, some other request just assigned the vehicle — for an
        // idempotent seed that is success, not a conflict.
        await assignments.TryAddAsync(assignmentResult.Value, cancellationToken);

        return Result.Success(planId.Value);
    }

    private async Task<Vehicle?> FindSeedVehicleAsync(CancellationToken cancellationToken)
    {
        var vinResult = Vin.Create(TransitSeverePlanSeed.Vin);
        var vehicle = vinResult.IsSuccess
            ? await vehicles.GetByVinAsync(vinResult.Value, cancellationToken)
            : null;

        return vehicle ?? await vehicles.GetByUnitNumberAsync(TransitSeverePlanSeed.UnitNumber, cancellationToken);
    }
}
