using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.Update;

public sealed class UpdateVehicleCommandHandler(IVehicleRepository repository)
    : ICommandHandler<UpdateVehicleCommand>
{
    public async Task<Result> Handle(UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(VehicleErrors.NotFound);
        }

        var vinResult = Vin.Create(command.Vin);
        if (vinResult.IsFailure)
        {
            return Result.Failure(vinResult.Error);
        }

        if (await repository.ExistsByVinAsync(vinResult.Value, vehicle.Id, cancellationToken))
        {
            return Result.Failure(VehicleErrors.DuplicateVin);
        }

        if (await repository.ExistsByUnitNumberAsync(command.UnitNumber.Trim(), vehicle.Id, cancellationToken))
        {
            return Result.Failure(VehicleErrors.DuplicateUnitNumber);
        }

        var result = vehicle.UpdateDetails(
            command.UnitNumber,
            vinResult.Value,
            command.Make,
            command.Model,
            command.Year,
            command.SeatingCapacity,
            command.LicencePlate,
            command.RequiredLicenceClass,
            command.AcquisitionCostCad,
            command.EndOfLifeKm);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
