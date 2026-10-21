using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Vehicles.SetMotiveId;

public sealed class SetVehicleMotiveIdCommandHandler(IVehicleRepository repository)
    : ICommandHandler<SetVehicleMotiveIdCommand>
{
    public async Task<Result> Handle(SetVehicleMotiveIdCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(VehicleErrors.NotFound);
        }

        var result = vehicle.SetMotiveVehicleId(command.MotiveVehicleId);
        if (result.IsFailure)
        {
            return result;
        }

        // Unique per tenant. The partial unique index enforces this in the database too, but a
        // raw unique violation would surface as a 500; checking here turns it into a 409. Write
        // side (tenant-filtered), so a link made seconds ago is already visible. Checked after
        // the aggregate has validated and trimmed the value, so " 123 " collides with "123".
        if (vehicle.MotiveVehicleId is { } motiveVehicleId
            && await repository.ExistsByMotiveVehicleIdAsync(motiveVehicleId, vehicle.Id, cancellationToken))
        {
            return Result.Failure(VehicleErrors.DuplicateMotiveVehicleId);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
