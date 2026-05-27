using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

internal sealed class UpdateVehicleCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<UpdateVehicleCommand>
{
    public async Task Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.Id} not found.");

        // Check uniqueness excluding the current vehicle
        if (!vehicle.UnitCode.Equals(request.UnitCode, StringComparison.OrdinalIgnoreCase) &&
            await vehicleRepository.ExistsByUnitCodeAsync(request.UnitCode, cancellationToken))
            throw new ConflictException($"A vehicle with unit code '{request.UnitCode}' already exists.");

        if (!vehicle.VIN.Equals(request.VIN, StringComparison.OrdinalIgnoreCase) &&
            await vehicleRepository.ExistsByVinAsync(request.VIN, cancellationToken))
            throw new ConflictException($"A vehicle with VIN '{request.VIN}' already exists.");

        if (!vehicle.LicensePlate.Equals(request.LicensePlate, StringComparison.OrdinalIgnoreCase) &&
            await vehicleRepository.ExistsByLicensePlateAsync(request.LicensePlate, cancellationToken))
            throw new ConflictException($"A vehicle with license plate '{request.LicensePlate}' already exists.");

        vehicle.Update(
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
            request.IsActive,
            request.Notes);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}
