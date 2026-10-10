using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.GetTripById;

public sealed class GetTripByIdQueryHandler(ITripReadService readService, TripOperatorAccess operatorAccess)
    : IQueryHandler<GetTripByIdQuery, TripResponse>
{
    public async Task<Result<TripResponse>> Handle(GetTripByIdQuery query, CancellationToken cancellationToken)
    {
        var trip = await readService.GetTripAsync(query.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure<TripResponse>(TripErrors.NotFound);
        }

        // The identity gate: this route is on the DriverAccess group, so a driver may read only
        // a trip assigned to them. 403 rather than 404 — the trip exists.
        if (!await operatorAccess.MayOperateAsync(trip.DriverId, cancellationToken))
        {
            return Result.Failure<TripResponse>(TripErrors.NotYourTrip);
        }

        return Result.Success(trip);
    }
}
