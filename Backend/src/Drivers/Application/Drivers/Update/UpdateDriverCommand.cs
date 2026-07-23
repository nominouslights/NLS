using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.Update;

/// <summary>Updates a driver's registration details. Status changes through its own command.</summary>
public sealed record UpdateDriverCommand(
    Guid TenantId,
    Guid DriverId,
    string Name,
    string? Phone,
    string LicenceClass,
    DateOnly? LicenceExpiry,
    string Source,
    bool HasWorkPermit) : ICommand;
