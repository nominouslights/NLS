using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Stops.GetStops;

public sealed class GetStopsQueryHandler(IStopReadService readService)
    : IQueryHandler<GetStopsQuery, IReadOnlyList<StopResponse>>
{
    public async Task<Result<IReadOnlyList<StopResponse>>> Handle(
        GetStopsQuery query,
        CancellationToken cancellationToken)
    {
        var stops = await readService.GetStopsAsync(cancellationToken);
        return Result.Success(stops);
    }
}
