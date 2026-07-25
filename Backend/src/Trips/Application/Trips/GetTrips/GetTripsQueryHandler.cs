using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Trips.GetTrips;

public sealed class GetTripsQueryHandler(ITripReadService readService)
    : IQueryHandler<GetTripsQuery, IReadOnlyList<TripResponse>>
{
    public async Task<Result<IReadOnlyList<TripResponse>>> Handle(
        GetTripsQuery query,
        CancellationToken cancellationToken)
    {
        var trips = await readService.GetTripsAsync(query.Filter, cancellationToken);
        return Result.Success(trips);
    }
}
