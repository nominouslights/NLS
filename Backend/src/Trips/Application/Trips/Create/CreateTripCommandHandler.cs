using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Create;

public sealed class CreateTripCommandHandler(
    ITripRepository tripRepository,
    IRouteRepository routeRepository,
    IDriverLookupRepository driverLookup,
    IVehicleLookupRepository vehicleLookup,
    ITripNumberGenerator tripNumberGenerator)
    : ICommandHandler<CreateTripCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTripCommand command, CancellationToken cancellationToken)
    {
        // Route snapshot: a referenced route wins over the free-form corridor fields.
        Guid? routeId = null;
        var routeName = command.RouteName ?? string.Empty;
        var origin = command.Origin ?? string.Empty;
        var destination = command.Destination ?? string.Empty;
        var stops = command.Stops;
        var distanceKm = command.DistanceKm;

        if (command.RouteId is { } requestedRouteId)
        {
            var route = await routeRepository.GetByIdAsync(requestedRouteId, cancellationToken);
            if (route is null)
            {
                return Result.Failure<Guid>(RouteErrors.NotFound);
            }

            routeId = route.Id;
            routeName = route.Name;
            origin = route.Origin;
            destination = route.Destination;
            stops = route.Stops;
            distanceKm = route.DistanceKm;
        }

        // Driver snapshot: a trip is never created unassigned — the driver is required and
        // goes through the same lookup validation as POST /assign (exists + Active in
        // driver_lookup).
        if (command.DriverId is not { } driverId)
        {
            return Result.Failure<Guid>(TripErrors.DriverRequired);
        }

        var driver = await driverLookup.GetAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<Guid>(TripErrors.DriverNotFound);
        }

        if (!driver.IsActive)
        {
            return Result.Failure<Guid>(TripErrors.DriverNotActive);
        }

        // Vehicle snapshot: likewise required, and it must be a real fleet vehicle — the
        // free-form unit-with-no-id path is gone (it let ghost units like "U-99" onto the
        // board). The unit number AND seating capacity are snapshotted from vehicle_lookup;
        // the derived capacity always wins over any client-supplied value.
        if (command.VehicleId is not { } vehicleId)
        {
            return Result.Failure<Guid>(TripErrors.VehicleRequired);
        }

        var vehicle = await vehicleLookup.GetAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure<Guid>(TripErrors.VehicleNotFound);
        }

        if (!vehicle.IsActive)
        {
            return Result.Failure<Guid>(TripErrors.VehicleNotActive);
        }

        var tripNumber = await tripNumberGenerator.NextAsync(command.TenantId, cancellationToken);

        var tripResult = Trip.Schedule(
            command.TenantId,
            tripNumber,
            command.ServiceDate,
            command.WindowStart,
            command.WindowEnd,
            command.ServiceType,
            routeId,
            routeName,
            origin,
            destination,
            stops,
            distanceKm,
            scheduleTemplateId: null,
            roundTripKey: null,
            command.Direction,
            command.IsEmptyLeg,
            command.ClientId,
            command.ClientName,
            command.PoNumber,
            driverId,
            driver.Name,
            vehicleId,
            vehicle.UnitNumber,
            vehicle.SeatingCapacity,
            command.SeatsMinimum);

        if (tripResult.IsFailure)
        {
            return Result.Failure<Guid>(tripResult.Error);
        }

        tripRepository.Add(tripResult.Value);
        await tripRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(tripResult.Value.Id);
    }
}
