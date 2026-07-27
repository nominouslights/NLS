using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Inspections.PropagateOdometer;

/// <summary>
/// Advances the linked vehicle's odometer from an inspection reading, intra-Fleet (no
/// cross-module traffic). Runs under the tenant the projection worker set from the journal row,
/// and is idempotent-safe: the monotonic <see cref="Vehicle.RecordOdometer"/> no-ops a reading
/// that is not ahead of the current value, so a redelivered event cannot double-count. When the
/// reading crosses end of life the aggregate auto-retires and the retirement certificate is
/// issued exactly as for a manual retire. A rejected (non-monotonic) or otherwise unrecordable
/// reading is swallowed — the inspection already stands as the record of what was read.
/// </summary>
public sealed class PropagateInspectionOdometerCommandHandler(
    IVehicleInspectionRepository inspections,
    IVehicleRepository vehicles)
    : ICommandHandler<PropagateInspectionOdometerCommand>
{
    public async Task<Result> Handle(PropagateInspectionOdometerCommand command, CancellationToken cancellationToken)
    {
        var inspection = await inspections.GetByIdAsync(command.InspectionId, cancellationToken);
        if (inspection is null || inspection.OdometerKm is not { } odometerKm)
        {
            // Not this tenant's row, or no reading to propagate — nothing to do.
            return Result.Success();
        }

        var vehicle = inspection.VehicleId is { } vehicleId
            ? await vehicles.GetByIdAsync(vehicleId, cancellationToken)
            : await vehicles.GetByUnitNumberAsync(inspection.Unit, cancellationToken);
        if (vehicle is null)
        {
            // No matching asset (unit-only entry for a vehicle not in this tenant's fleet).
            return Result.Success();
        }

        var wasRetired = vehicle.Status == VehicleStatus.Retired;

        var recorded = vehicle.RecordOdometer(odometerKm);
        if (recorded.IsFailure)
        {
            // Non-monotonic reading (or a disposed vehicle) — ignore gracefully; the inspection
            // still stands. Nothing changed on the aggregate, so there is nothing to save.
            return Result.Success();
        }

        if (!wasRetired && vehicle.Status == VehicleStatus.Retired)
        {
            await RetirementCertificateIssuer.IssueAsync(vehicle, vehicles, cancellationToken);
        }

        await vehicles.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
