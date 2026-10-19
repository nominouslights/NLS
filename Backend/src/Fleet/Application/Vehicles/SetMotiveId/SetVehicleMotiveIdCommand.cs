using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Vehicles.SetMotiveId;

/// <summary>
/// Sets (non-null) or clears (null) a vehicle's Motive id — the per-vehicle "regulated" flag.
/// <c>PUT /api/fleet/vehicles/{id}/motive</c> sends the value, <c>DELETE</c> sends null.
/// </summary>
public sealed record SetVehicleMotiveIdCommand(
    Guid TenantId,
    Guid VehicleId,
    string? MotiveVehicleId) : ICommand;
