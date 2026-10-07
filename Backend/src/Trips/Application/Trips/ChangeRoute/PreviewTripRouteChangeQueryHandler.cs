using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

public sealed class PreviewTripRouteChangeQueryHandler(TripRouteChangeImpactCalculator calculator)
    : IQueryHandler<PreviewTripRouteChangeQuery, TripRouteChangePreviewResponse>
{
    public async Task<Result<TripRouteChangePreviewResponse>> Handle(
        PreviewTripRouteChangeQuery query, CancellationToken cancellationToken)
    {
        var impact = await calculator.CalculateAsync(query.TripId, query.RouteId, cancellationToken);
        return impact.IsFailure
            ? Result.Failure<TripRouteChangePreviewResponse>(impact.Error)
            : Result.Success(TripRouteChangePreviewResponse.From(impact.Value));
    }
}
