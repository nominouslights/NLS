using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.RecordOdometer;

/// <summary>
/// Records the reading via <see cref="Vehicle.RecordOdometer"/>; when the aggregate
/// auto-retires (end of service life crossed), the retirement certificate is issued
/// exactly as for a manual retire.
/// </summary>
public sealed class RecordOdometerCommandHandler(IVehicleRepository repository)
    : ICommandHandler<RecordOdometerCommand>
{
    public async Task<Result> Handle(RecordOdometerCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(VehicleErrors.NotFound);
        }

        var wasRetired = vehicle.Status == VehicleStatus.Retired;

        var result = vehicle.RecordOdometer(command.OdometerKm);
        if (result.IsFailure)
        {
            return result;
        }

        if (!wasRetired && vehicle.Status == VehicleStatus.Retired)
        {
            await RetirementCertificateIssuer.IssueAsync(vehicle, repository, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
