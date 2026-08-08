using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Assign;

public sealed class AssignTripCommandHandler(
    ITripRepository tripRepository,
    IDriverLookupRepository driverLookup,
    IVehicleLookupRepository vehicleLookup)
    : ICommandHandler<AssignTripCommand>
{
    public async Task<Result> Handle(AssignTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        Result driverResult;
        if (command.DriverId is { } driverId)
        {
            var driver = await driverLookup.GetAsync(driverId, cancellationToken);
            if (driver is null)
            {
                return Result.Failure(TripErrors.DriverNotFound);
            }

            if (!driver.IsActive)
            {
                return Result.Failure(TripErrors.DriverNotActive);
            }

            driverResult = trip.AssignDriver(driverId, driver.Name);
        }
        else
        {
            driverResult = trip.UnassignDriver();
        }

        if (driverResult.IsFailure)
        {
            return driverResult;
        }

        // Vehicle snapshot: a supplied VehicleId goes through the same lookup validation as
        // the driver — the vehicle must exist and be Active in vehicle_lookup, and its unit
        // number and seating capacity are snapshotted from the lookup (the domain refuses a
        // vehicle seating fewer than SeatsConfirmed). A free-form unit with no id passes
        // through and leaves the trip's capacity as it was.
        var vehicleUnit = command.VehicleUnit;
        int? seatingCapacity = null;
        if (command.VehicleId is { } vehicleId)
        {
            var vehicle = await vehicleLookup.GetAsync(vehicleId, cancellationToken);
            if (vehicle is null)
            {
                return Result.Failure(TripErrors.VehicleNotFound);
            }

            if (!vehicle.IsActive)
            {
                return Result.Failure(TripErrors.VehicleNotActive);
            }

            vehicleUnit = vehicle.UnitNumber;
            seatingCapacity = vehicle.SeatingCapacity;
        }

        var vehicleResult = trip.AssignVehicle(command.VehicleId, vehicleUnit, seatingCapacity);
        if (vehicleResult.IsFailure)
        {
            return vehicleResult;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
