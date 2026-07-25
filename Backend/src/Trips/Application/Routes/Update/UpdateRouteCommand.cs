using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes.Update;

/// <summary>
/// Full-row route edit (including active/inactive). Trips already generated keep their
/// snapshot; templates pick the change up at their next generation.
/// </summary>
public sealed record UpdateRouteCommand(
    Guid RouteId,
    string Name,
    IReadOnlyList<RouteStop> Stops,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active) : ICommand;
