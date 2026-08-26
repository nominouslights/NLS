using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Riders.GetRiders;

public sealed class GetRidersQueryHandler(IRiderReadService readService)
    : IQueryHandler<GetRidersQuery, IReadOnlyList<RiderResponse>>
{
    public async Task<Result<IReadOnlyList<RiderResponse>>> Handle(
        GetRidersQuery query,
        CancellationToken cancellationToken)
    {
        var riders = await readService.GetRidersAsync(query.ServiceType, query.Search, cancellationToken);
        return Result.Success(riders);
    }
}
