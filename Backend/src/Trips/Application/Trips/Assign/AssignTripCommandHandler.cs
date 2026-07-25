using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Assign;

public sealed class AssignTripCommandHandler(
    ITripRepository tripRepository,
    IDriverLookupRepository driverLookup)
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

        var vehicleResult = trip.AssignVehicle(command.VehicleUnit);
        if (vehicleResult.IsFailure)
        {
            return vehicleResult;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
