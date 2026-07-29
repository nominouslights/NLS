using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Routes.Create;

/// <summary>
/// Creates a route (active by default) from an ordered list of catalog stop ids. Duration
/// arrives as whole minutes. The handler resolves the stops (same tenant, same domain) and
/// snapshots their name + coordinates onto the route.
/// </summary>
public sealed record CreateRouteCommand(
    Guid TenantId,
    string Name,
    IReadOnlyList<Guid> StopIds,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass) : ICommand<Guid>;
