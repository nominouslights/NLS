using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.GetForDriver;

/// <summary>Lists a driver's credentials, newest issued first.</summary>
public sealed record GetDriverCredentialsQuery(Guid TenantId, Guid DriverId)
    : IQuery<IReadOnlyList<DriverCredentialResponse>>;
