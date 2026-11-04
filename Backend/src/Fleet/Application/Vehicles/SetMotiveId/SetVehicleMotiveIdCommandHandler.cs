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

        // Unique per tenant. Write side (tenant-filtered), so a link made seconds ago is already
        // visible. Checked after the aggregate has validated and trimmed the value, so " 123 "
        // collides with "123".
        if (vehicle.MotiveVehicleId is { } motiveVehicleId
            && await repository.ExistsByMotiveVehicleIdAsync(motiveVehicleId, vehicle.Id, cancellationToken))
        {
            return Result.Failure(VehicleErrors.DuplicateMotiveVehicleId);
        }

        // The check and the save are not atomic: two concurrent links can both pass it. The
        // partial unique index then refuses the loser, and the repository translates that one
        // constraint into the same 409 rather than letting it surface as a 500.
        return await repository.TrySaveMotiveVehicleIdAsync(cancellationToken)
            ? Result.Success()
            : Result.Failure(VehicleErrors.DuplicateMotiveVehicleId);
    }
}
