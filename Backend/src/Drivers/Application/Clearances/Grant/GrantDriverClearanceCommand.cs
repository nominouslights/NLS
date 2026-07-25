using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Clearances.Grant;

/// <summary>Grants a client-site clearance to a driver. Returns the new clearance's id.</summary>
public sealed record GrantDriverClearanceCommand(
    Guid TenantId,
    Guid DriverId,
    string Title,
    string ClientName,
    DateOnly? Expiry) : ICommand<Guid>;
