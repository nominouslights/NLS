using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Routes.Create;

/// <summary>
/// Creates a route (active by default) from an ordered list of catalog stops. Duration arrives
/// as whole minutes. The handler resolves the stops (same tenant, same domain) and snapshots
/// their name + coordinates onto the route, alongside each stop's optional timetable offsets.
/// </summary>
public sealed record CreateRouteCommand(
    Guid TenantId,
    string Name,
    IReadOnlyList<RouteStopInput> Stops,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass) : ICommand<Guid>;
