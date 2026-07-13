using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles.ChangeStatus;

/// <summary>
/// Moves a vehicle through the lifecycle matrix (Active / InMaintenance / OutOfService /
/// Retired). Sold and Recycled are rejected here — use <c>DisposeVehicleCommand</c>.
/// OutOfService requires a non-empty <paramref name="Reason"/>.
/// </summary>
public sealed record ChangeVehicleStatusCommand(
    Guid TenantId,
    Guid VehicleId,
    VehicleStatus Status,
    string? Reason) : ICommand;
