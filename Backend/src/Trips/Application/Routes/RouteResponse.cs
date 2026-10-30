namespace NorthernLink.Trips.Application.Routes;

/// <summary>
/// Public contract for a route (RoutesSchedules left panel). Duration travels as whole
/// minutes; origin/destination (and their provinces) are derived from the first/last stop.
/// </summary>
public sealed record RouteResponse(
    Guid Id,
    string Name,
    IReadOnlyList<RouteStopResponse> Stops,
    string Origin,
    string OriginProvince,
    string Destination,
    string DestinationProvince,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// One ordered stop on a route. Carries the catalog reference (<see cref="StopId"/>) and
/// the snapshotted coordinates when built from the stop catalog; all three are null for
/// legacy free-text stops. <see cref="Province"/> is the stop's two-letter province code and
/// is never null — a snapshot written before provinces were recorded reads as "MB", the
/// only province a route may run in. The two offsets are the stop's timetable entry — minutes
/// after the respective leg's departure — and are null throughout on a route with no timetable.
/// </summary>
public sealed record RouteStopResponse(
    Guid? StopId,
    string Name,
    int Order,
    double? Latitude,
    double? Longitude,
    string Province,
    int? OutboundOffsetMinutes,
    int? ReturnOffsetMinutes);
