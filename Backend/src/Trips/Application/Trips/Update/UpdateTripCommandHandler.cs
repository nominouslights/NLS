using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.Update;

public sealed class UpdateTripCommandHandler(ITripRepository tripRepository)
    : ICommandHandler<UpdateTripCommand>
{
    public async Task<Result> Handle(UpdateTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        // A different route goes through ChangeTripRouteCommand, which also moves the paired
        // leg and checks passengers and cargo first. Rejected here before anything else so the
        // caller learns the right endpoint rather than a validation error about corridor text.
        if (command.RouteId != trip.RouteId)
        {
            return Result.Failure(TripErrors.UseChangeRoute);
        }

        var routeName = command.RouteName ?? string.Empty;
        var origin = command.Origin ?? string.Empty;
        var destination = command.Destination ?? string.Empty;
        var stops = command.Stops;
        var distanceKm = command.DistanceKm;

        if (trip.RouteId is not null)
        {
            // Same catalogue route: keep the trip's own snapshot. The Dispatcher's edit form
            // always re-sends the current routeId, so re-snapshotting the catalogue here would
            // flip an Inbound leg to outbound order and pull in later catalogue edits on an
            // unrelated change (a PO number, a time).
            routeName = trip.RouteName;
            origin = trip.Origin;
            destination = trip.Destination;
            stops = trip.Stops;
            distanceKm = trip.DistanceKm;
        }

        var result = trip.Update(
            command.ServiceDate,
            command.WindowStart,
            command.WindowEnd,
            command.ServiceType,
            trip.RouteId,
            routeName,
            origin,
            destination,
            stops,
            distanceKm,
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
