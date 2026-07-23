using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes.Create;

/// <summary>Creates a route (active by default). Duration arrives as whole minutes.</summary>
public sealed record CreateRouteCommand(
    Guid TenantId,
    string Name,
    IReadOnlyList<RouteStop> Stops,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass) : ICommand<Guid>;
