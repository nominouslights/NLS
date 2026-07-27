using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Stops.Update;

/// <summary>
/// Full-row stop edit (including active/inactive). Routes that already snapshotted this
/// stop keep their coordinates; new route builds pick the change up.
/// </summary>
public sealed record UpdateStopCommand(
    Guid StopId,
    string Name,
    StopType? Type,
    string? Street,
    string City,
    string Province,
    string? PostalCode,
    string Country,
    double Latitude,
    double Longitude,
    string? Notes,
    bool Active) : ICommand;
