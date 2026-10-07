using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
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

        if (command.RouteId is { } requestedRouteId && requestedRouteId == trip.RouteId)
        {
            // Same route: keep the trip's own snapshot. The Dispatcher's edit form always
            // re-sends the current routeId, so re-snapshotting the catalogue here would flip
            // an Inbound leg to outbound order and pull in later catalogue edits on an
            // unrelated change (a PO number, a time).
            routeId = trip.RouteId;
            routeName = trip.RouteName;
            origin = trip.Origin;
            destination = trip.Destination;
            stops = trip.Stops;
            distanceKm = trip.DistanceKm;
        }
        else if (command.RouteId is { } newRouteId)
        {
            var route = await routeRepository.GetByIdAsync(newRouteId, cancellationToken);
            if (route is null)
            {
                return Result.Failure(RouteErrors.NotFound);
            }

            // A genuine re-route snapshots the catalogue route oriented for this leg: an
            // Inbound trip runs it backwards, exactly as generation does.
            var inbound = trip.Direction == TripDirection.Inbound;
            routeId = route.Id;
            routeName = route.Name;
            origin = inbound ? route.Destination : route.Origin;
            destination = inbound ? route.Origin : route.Destination;
            stops = RouteStop.OrientedFor(route.Stops, trip.Direction);
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
