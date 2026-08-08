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

        // Driver snapshot: assignment at creation goes through the same lookup validation
        // as POST /assign — the driver must exist and be Active in driver_lookup.
        string? driverName = null;
        if (command.DriverId is { } driverId)
        {
            var driver = await driverLookup.GetAsync(driverId, cancellationToken);
            if (driver is null)
            {
                return Result.Failure<Guid>(TripErrors.DriverNotFound);
            }

            if (!driver.IsActive)
            {
                return Result.Failure<Guid>(TripErrors.DriverNotActive);
            }

            driverName = driver.Name;
        }

        // Vehicle snapshot: assignment at creation goes through the same lookup validation
        // as POST /assign — the vehicle must exist and be Active in vehicle_lookup, and its
        // unit number AND seating capacity are snapshotted from the lookup (the derived
        // capacity wins over any client-supplied value). A free-form unit with no id passes,
        // keeping the manual capacity.
        var vehicleUnit = command.VehicleUnit;
        var seatsCapacity = command.SeatsCapacity;
        if (command.VehicleId is { } vehicleId)
        {
            var vehicle = await vehicleLookup.GetAsync(vehicleId, cancellationToken);
            if (vehicle is null)
            {
                return Result.Failure<Guid>(TripErrors.VehicleNotFound);
            }

            if (!vehicle.IsActive)
            {
                return Result.Failure<Guid>(TripErrors.VehicleNotActive);
            }

            vehicleUnit = vehicle.UnitNumber;
            seatsCapacity = vehicle.SeatingCapacity;
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
            command.DriverId,
            driverName,
            command.VehicleId,
            vehicleUnit,
            seatsCapacity,
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
