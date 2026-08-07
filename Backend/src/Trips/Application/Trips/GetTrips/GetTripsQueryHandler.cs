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
        var (trips, totalCount) = await readService.GetTripsAsync(query.Filter, cancellationToken);

        // Paging rides along on the Result rather than in the response type, so this query
        // keeps the same IQuery<IReadOnlyList<TripResponse>> shape as every other list query.
        return Result.Success(trips)
            .WithPage(new PageInfo(query.Filter.Page, query.Filter.PageSize, totalCount));
    }
}
