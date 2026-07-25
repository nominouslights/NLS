using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Update;

public sealed class UpdateTripCommandHandler(
    ITripRepository tripRepository,
    IRouteRepository routeRepository)
    : ICommandHandler<UpdateTripCommand>
{
    public async Task<Result> Handle(UpdateTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

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
                return Result.Failure(RouteErrors.NotFound);
            }

            routeId = route.Id;
            routeName = route.Name;
            origin = route.Origin;
            destination = route.Destination;
            stops = route.Stops;
            distanceKm = route.DistanceKm;
        }

        var result = trip.Update(
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
            command.IsEmptyLeg,
            command.ClientId,
            command.ClientName,
            command.PoNumber,
            command.SeatsCapacity,
            command.SeatsMinimum);

        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
