namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

/// <summary>
/// Public contract for <c>GET /api/trips/{id}/change-route/preview?routeId=</c> — what
/// <c>POST /api/trips/{id}/change-route</c> would do, nothing persisted. Always 200 for an
/// existing trip: problems arrive as <see cref="Blockers"/> (same codes and messages the POST
/// returns as its error), not as an error status, so the dialog can show all of them at once.
/// <see cref="RequiresAcknowledgement"/> is true exactly when <see cref="Warnings"/> is
/// non-empty — the POST then needs <c>acknowledgeWarnings: true</c>. <see cref="Notices"/> are
/// informational and never need acknowledging.
/// </summary>
public sealed record TripRouteChangePreviewResponse(
    Guid TripId,
    string TripNumber,
    Guid? CurrentRouteId,
    string CurrentRouteName,
    Guid NewRouteId,
    string? NewRouteName,
    bool CanChange,
    bool RequiresAcknowledgement,
    TripRouteChangePartnerResponse? Partner,
    IReadOnlyList<TripRouteChangeLegResponse> Legs,
    IReadOnlyList<TripRouteChangeFindingResponse> Blockers,
    IReadOnlyList<TripRouteChangeFindingResponse> Warnings,
    IReadOnlyList<TripRouteChangeFindingResponse> Notices)
{
    public static TripRouteChangePreviewResponse From(TripRouteChangeImpact impact)
    {
        var partner = impact.Partner;
        return new TripRouteChangePreviewResponse(
            impact.Trip.Id,
            impact.Trip.TripNumber,
            impact.Trip.RouteId,
            impact.Trip.RouteName,
            impact.RequestedRouteId,
            impact.Route?.Name,
            impact.CanChange,
            impact.RequiresAcknowledgement,
            partner is null
                ? null
                : new TripRouteChangePartnerResponse(
                    partner.Id, partner.TripNumber, partner.Status.ToString(), partner.Direction?.ToString()),
            [.. impact.Legs.Select(TripRouteChangeLegResponse.From)],
            [.. impact.Blockers.Select(TripRouteChangeFindingResponse.From)],
            [.. impact.Warnings.Select(TripRouteChangeFindingResponse.From)],
            [.. impact.Notices.Select(TripRouteChangeFindingResponse.From)]);
    }
}

/// <summary>The paired leg of a round trip — the one that always moves with the requested trip.</summary>
public sealed record TripRouteChangePartnerResponse(
    Guid TripId,
    string TripNumber,
    string Status,
    string? Direction);

/// <summary>
/// One leg as it is now and as it would be. The <c>new*</c> corridor fields are null when the
/// target route does not exist. <see cref="WillChange"/> is false for a paired leg already on
/// the target route. <see cref="NewWindowEnd"/> is departure + the route's estimated duration
/// when the leg has a window end today, and null when its window is open-ended (it stays
/// open-ended).
/// </summary>
public sealed record TripRouteChangeLegResponse(
    Guid TripId,
    string TripNumber,
    bool IsRequestedTrip,
    string Status,
    string? Direction,
    bool WillChange,
    string CurrentRouteName,
    string CurrentOrigin,
    string CurrentDestination,
    int CurrentDistanceKm,
    string? NewOrigin,
    string? NewDestination,
    int? NewDistanceKm,
    IReadOnlyList<TripStopResponse>? NewStops,
    TimeOnly WindowStart,
    TimeOnly? CurrentWindowEnd,
    TimeOnly? NewWindowEnd)
{
    public static TripRouteChangeLegResponse From(TripRouteChangeLeg leg)
    {
        var trip = leg.Trip;
        var snapshot = leg.NewSnapshot;
        return new TripRouteChangeLegResponse(
            trip.Id,
            trip.TripNumber,
            leg.IsRequestedTrip,
            trip.Status.ToString(),
            trip.Direction?.ToString(),
            leg.WillChange,
            trip.RouteName,
            trip.Origin,
            trip.Destination,
            trip.DistanceKm,
            snapshot?.Origin,
            snapshot?.Destination,
            snapshot?.DistanceKm,
            snapshot is null
                ? null
                : [.. snapshot.Stops.OrderBy(stop => stop.Order).Select(stop => new TripStopResponse(
                    stop.Name,
                    stop.Order,
                    stop.StopId,
                    stop.Latitude,
                    stop.Longitude,
                    stop.OutboundOffsetMinutes,
                    stop.ReturnOffsetMinutes))],
            trip.WindowStart,
            trip.WindowEnd,
            snapshot is null || !leg.WillChange ? trip.WindowEnd : snapshot.WindowEnd);
    }
}

/// <summary>
/// A blocker, warning, or notice. <see cref="TripId"/>/<see cref="TripNumber"/> name the leg it
/// is about (null when it is about the request as a whole); <see cref="Count"/> is set for
/// countable warnings (passengers, shipment legs).
/// </summary>
public sealed record TripRouteChangeFindingResponse(
    string Code,
    string Message,
    Guid? TripId,
    string? TripNumber,
    int? Count)
{
    public static TripRouteChangeFindingResponse From(TripRouteChangeFinding finding) =>
        new(finding.Code, finding.Message, finding.TripId, finding.TripNumber, finding.Count);
}
