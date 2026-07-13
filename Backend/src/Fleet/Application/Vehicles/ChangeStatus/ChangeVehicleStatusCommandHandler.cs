using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.ChangeStatus;

/// <summary>
/// Applies a status transition; when the vehicle enters Retired, a retirement
/// certificate is issued in the same unit of work.
/// </summary>
public sealed class ChangeVehicleStatusCommandHandler(IVehicleRepository repository)
    : ICommandHandler<ChangeVehicleStatusCommand>
{
    public async Task<Result> Handle(ChangeVehicleStatusCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(VehicleErrors.NotFound);
        }

        var wasRetired = vehicle.Status == VehicleStatus.Retired;

        var result = vehicle.ChangeStatus(command.Status, command.Reason);
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
