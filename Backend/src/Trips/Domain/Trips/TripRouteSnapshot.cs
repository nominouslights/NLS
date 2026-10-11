using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// The route snapshot a trip would carry on a catalogue route, oriented for that trip's leg —
/// what <see cref="Trip.ChangeRoute"/> writes and what the change preview shows before it does.
/// <see cref="OriginProvince"/>/<see cref="DestinationProvince"/> are the endpoint stops'
/// province codes (the route's first/last stop, swapped for an Inbound leg).
/// <see cref="WindowEnd"/> is the window end the change would leave the trip with (null when the
/// trip's window is open-ended).
/// </summary>
public sealed record TripRouteSnapshot(
    Guid RouteId,
    string RouteName,
    string Origin,
    string OriginProvince,
    string Destination,
    string DestinationProvince,
    IReadOnlyList<RouteStop> Stops,
    int DistanceKm,
    TimeOnly? WindowEnd);
