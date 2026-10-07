using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

/// <summary>
/// What <see cref="ChangeTripRouteCommand"/> would do for the same trip and route — same
/// calculator, nothing persisted. 404 only when the trip itself does not exist; every other
/// problem is a blocker in the response.
/// </summary>
public sealed record PreviewTripRouteChangeQuery(Guid TripId, Guid RouteId) : IQuery<TripRouteChangePreviewResponse>;
