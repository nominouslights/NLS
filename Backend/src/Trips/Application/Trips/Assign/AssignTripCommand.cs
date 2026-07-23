using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.Assign;

/// <summary>
/// Sets a trip's coverage in one shot: a null <see cref="DriverId"/> unassigns the
/// driver, a null <see cref="VehicleUnit"/> clears the vehicle. A non-null driver is
/// validated against driver_lookup (must exist and be Active) and the name snapshotted.
/// </summary>
public sealed record AssignTripCommand(
    Guid TripId,
    Guid? DriverId,
    string? VehicleUnit) : ICommand;
