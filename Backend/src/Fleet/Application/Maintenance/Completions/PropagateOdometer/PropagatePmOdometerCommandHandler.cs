using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Maintenance.Completions.PropagateOdometer;

/// <summary>
/// Advances the vehicle's odometer from a PM completion's reading, intra-Fleet (no
/// cross-module traffic) — modeled exactly on
/// <c>PropagateInspectionOdometerCommandHandler</c>. Runs under the tenant the projection
/// worker set from the journal row, and is idempotent-safe: the monotonic
/// <see cref="Vehicle.RecordOdometer"/> no-ops a reading that is not ahead of the current
/// value, so a redelivered event cannot double-count. When the reading crosses end of life
/// the aggregate auto-retires and the retirement certificate is issued exactly as for a
/// manual retire. A rejected (non-monotonic) or otherwise unrecordable reading is
/// swallowed — the completion already stands as the record of what was read.
/// </summary>
public sealed class PropagatePmOdometerCommandHandler(
    IPmCompletionRepository completions,
    IVehicleRepository vehicles)
    : ICommandHandler<PropagatePmOdometerCommand>
{
    public async Task<Result> Handle(PropagatePmOdometerCommand command, CancellationToken cancellationToken)
    {
        var completion = await completions.GetByIdAsync(command.CompletionId, cancellationToken);
        if (completion is null)
        {
            // Not this tenant's row — nothing to do.
            return Result.Success();
        }

        var vehicle = await vehicles.GetByIdAsync(completion.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            // No matching asset — the completion still stands.
            return Result.Success();
        }

        var wasRetired = vehicle.Status == VehicleStatus.Retired;

        var recorded = vehicle.RecordOdometer(completion.OdometerKm);
        if (recorded.IsFailure)
        {
            // A historical (lower) reading or a disposed vehicle — ignore gracefully; the
            // completion still stands. Nothing changed on the aggregate, nothing to save.
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
