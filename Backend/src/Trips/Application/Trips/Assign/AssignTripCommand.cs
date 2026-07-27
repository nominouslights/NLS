using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.Assign;

/// <summary>
/// Sets a trip's coverage in one shot: a null <see cref="DriverId"/> unassigns the
/// driver, and a null <see cref="VehicleId"/> with a null <see cref="VehicleUnit"/>
/// clears the vehicle. A non-null driver is validated against driver_lookup (must exist
/// and be Active) and the name snapshotted; likewise a non-null <see cref="VehicleId"/>
/// is validated against vehicle_lookup and the unit-number snapshotted from it. A
/// free-form <see cref="VehicleUnit"/> with no id is still accepted as-is.
/// </summary>
public sealed record AssignTripCommand(
    Guid TripId,
    Guid? DriverId,
    Guid? VehicleId,
    string? VehicleUnit) : ICommand;
