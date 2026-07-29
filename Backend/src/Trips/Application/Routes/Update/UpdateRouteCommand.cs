using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Routes.Update;

/// <summary>
/// Full-row route edit (including active/inactive) from an ordered list of catalog stop
/// ids. Trips already generated keep their snapshot; templates pick the change up at their
/// next generation.
/// </summary>
public sealed record UpdateRouteCommand(
    Guid RouteId,
    string Name,
    IReadOnlyList<Guid> StopIds,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active) : ICommand;
