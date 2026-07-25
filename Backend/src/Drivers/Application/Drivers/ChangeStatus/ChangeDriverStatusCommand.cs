using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.ChangeStatus;

/// <summary>
/// Moves a driver through the lifecycle matrix (Active / Inactive / Deactivated).
/// A deactivated driver can only be reinstated to Active.
/// </summary>
public sealed record ChangeDriverStatusCommand(
    Guid TenantId,
    Guid DriverId,
    DriverStatus Status) : ICommand;
