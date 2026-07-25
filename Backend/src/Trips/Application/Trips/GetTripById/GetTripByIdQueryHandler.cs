using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.GetTripById;

public sealed class GetTripByIdQueryHandler(ITripReadService readService)
    : IQueryHandler<GetTripByIdQuery, TripResponse>
{
    public async Task<Result<TripResponse>> Handle(GetTripByIdQuery query, CancellationToken cancellationToken)
    {
        var trip = await readService.GetTripAsync(query.TripId, cancellationToken);
        return trip is null
            ? Result.Failure<TripResponse>(TripErrors.NotFound)
            : Result.Success(trip);
    }
}
