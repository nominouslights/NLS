namespace NorthernLink.Fleet.Domain.Vehicles;

/// <summary>
/// Lifecycle status of a vehicle. Only <see cref="Active"/> is trip-assignable.
/// <see cref="Retired"/> is the mandatory gateway to disposal; <see cref="Sold"/> and
/// <see cref="Recycled"/> are terminal. See <see cref="Vehicle.CanTransition"/> for the matrix.
/// </summary>
public enum VehicleStatus
{
    Active,
    InMaintenance,
    OutOfService,
    Retired,
    Sold,
    Recycled,
}
