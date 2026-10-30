using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Trips.GetTrips;

public sealed class GetTripsQueryHandler(ITripReadService readService, TripOperatorAccess operatorAccess)
    : IQueryHandler<GetTripsQuery, IReadOnlyList<TripResponse>>
{
    public async Task<Result<IReadOnlyList<TripResponse>>> Handle(
        GetTripsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = query.Filter;

        // The identity gate for the list: GET /api/trips sits on the DriverAccess group, so a
        // driver sees only their own trips. A ?driverId= naming anyone else is not overridden
        // but ANDed — it then matches nothing, the same answer a stranger's history deserves.
        var scope = await operatorAccess.ResolveScopeAsync(cancellationToken);
        if (!scope.IsUnrestricted)
        {
            if (scope.DriverId is not { } ownDriverId
                || (filter.DriverId is { } requested && requested != ownDriverId))
            {
                return Result.Success<IReadOnlyList<TripResponse>>([])
                    .WithPage(new PageInfo(filter.Page, filter.PageSize, 0));
            }

            filter = filter with { DriverId = ownDriverId };
        }

        var (trips, totalCount) = await readService.GetTripsAsync(filter, cancellationToken);

        // Paging rides along on the Result rather than in the response type, so this query
        // keeps the same IQuery<IReadOnlyList<TripResponse>> shape as every other list query.
        return Result.Success(trips)
            .WithPage(new PageInfo(filter.Page, filter.PageSize, totalCount));
    }
}
