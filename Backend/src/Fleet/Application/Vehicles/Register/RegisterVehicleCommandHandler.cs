using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.Register;

public sealed class RegisterVehicleCommandHandler(IVehicleRepository repository)
    : ICommandHandler<RegisterVehicleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterVehicleCommand command, CancellationToken cancellationToken)
    {
        var vinResult = Vin.Create(command.Vin);
        if (vinResult.IsFailure)
        {
            return Result.Failure<Guid>(vinResult.Error);
        }

        if (await repository.ExistsByVinAsync(vinResult.Value, null, cancellationToken))
        {
            return Result.Failure<Guid>(VehicleErrors.DuplicateVin);
        }

        if (await repository.ExistsByUnitNumberAsync(command.UnitNumber.Trim(), null, cancellationToken))
        {
            return Result.Failure<Guid>(VehicleErrors.DuplicateUnitNumber);
        }

        var vehicleResult = Vehicle.Register(
            command.TenantId,
            command.UnitNumber,
            vinResult.Value,
            command.Make,
            command.Model,
            command.Year,
            command.SeatingCapacity,
            command.LicencePlate,
            command.RequiredLicenceClass,
            command.OdometerKm,
            command.AcquisitionCostCad,
            command.EndOfLifeKm);

        if (vehicleResult.IsFailure)
        {
            return Result.Failure<Guid>(vehicleResult.Error);
        }

        repository.Add(vehicleResult.Value);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(vehicleResult.Value.Id);
    }
}
