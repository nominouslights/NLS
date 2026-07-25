namespace NorthernLink.Trips.Application.Routes;

/// <summary>
/// Public contract for a route (RoutesSchedules left panel). Duration travels as whole
/// minutes; origin/destination are derived from the first/last stop.
/// </summary>
public sealed record RouteResponse(
    Guid Id,
    string Name,
    IReadOnlyList<RouteStopResponse> Stops,
    string Origin,
    string Destination,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>One ordered stop on a route.</summary>
public sealed record RouteStopResponse(string Name, int Order);
