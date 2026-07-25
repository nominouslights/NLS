using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.Register;

/// <summary>
/// Registers a new driver on the roster. <paramref name="TenantId"/> is stamped by the
/// endpoint layer from the ambient tenant context. Returns the new driver's id.
/// </summary>
public sealed record RegisterDriverCommand(
    Guid TenantId,
    string Name,
    string? Phone,
    string LicenceClass,
    DateOnly? LicenceExpiry,
    string Source,
    bool HasWorkPermit) : ICommand<Guid>;
