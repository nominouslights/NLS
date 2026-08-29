using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Routes.Update;

/// <summary>
/// Full-row route edit (including active/inactive) from an ordered list of catalog stops and
/// their optional timetable offsets. Trips already generated keep their snapshot — a timetable
/// edit reaches only trips generated after it; templates pick the change up at their next
/// generation.
/// </summary>
public sealed record UpdateRouteCommand(
    Guid RouteId,
    string Name,
    IReadOnlyList<RouteStopInput> Stops,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active) : ICommand;
