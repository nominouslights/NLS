using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

internal sealed class CreateVehicleCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<CreateVehicleCommand, CreateVehicleResult>
{
    public async Task<CreateVehicleResult> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (await vehicleRepository.ExistsByUnitCodeAsync(request.UnitCode, cancellationToken))
            throw new ConflictException($"A vehicle with unit code '{request.UnitCode}' already exists.");

        if (await vehicleRepository.ExistsByVinAsync(request.VIN, cancellationToken))
            throw new ConflictException($"A vehicle with VIN '{request.VIN}' already exists.");

        if (await vehicleRepository.ExistsByLicensePlateAsync(request.LicensePlate, cancellationToken))
            throw new ConflictException($"A vehicle with license plate '{request.LicensePlate}' already exists.");

        var vehicle = Vehicle.Create(
            request.UnitCode,
            request.VIN,
            request.Make,
            request.Model,
            request.Year,
            request.Color,
            request.LicensePlate,
            request.Province,
            request.VehicleType,
            request.PassengerCapacity,
            request.CurrentOdometerKm,
            DateTime.SpecifyKind(request.AcquisitionDate, DateTimeKind.Utc),
            request.RegistrationExpiry.HasValue
                ? DateTime.SpecifyKind(request.RegistrationExpiry.Value, DateTimeKind.Utc)
                : null,
            request.InsuranceProvider,
            request.InsurancePolicyNumber,
            request.InsuranceExpiry.HasValue
                ? DateTime.SpecifyKind(request.InsuranceExpiry.Value, DateTimeKind.Utc)
                : null,
            request.Notes);

        await vehicleRepository.AddAsync(vehicle, cancellationToken);

        return new CreateVehicleResult(vehicle.Id);
    }
}
