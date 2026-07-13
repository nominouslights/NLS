using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Vehicles.RecordOdometer;

/// <summary>
/// Records a new (monotonic) odometer reading. Crossing the vehicle's end-of-life
/// threshold auto-retires it as a side effect.
/// </summary>
public sealed record RecordOdometerCommand(
    Guid TenantId,
    Guid VehicleId,
    int OdometerKm) : ICommand;
