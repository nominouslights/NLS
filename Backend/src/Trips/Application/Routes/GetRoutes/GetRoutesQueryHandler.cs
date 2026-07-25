using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Routes.GetRoutes;

public sealed class GetRoutesQueryHandler(IRouteReadService readService)
    : IQueryHandler<GetRoutesQuery, IReadOnlyList<RouteResponse>>
{
    public async Task<Result<IReadOnlyList<RouteResponse>>> Handle(
        GetRoutesQuery query,
        CancellationToken cancellationToken)
    {
        var routes = await readService.GetRoutesAsync(cancellationToken);
        return Result.Success(routes);
    }
}
