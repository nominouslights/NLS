using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.GetDrivers;

/// <summary>Lists every driver visible to the current tenant, ordered by name.</summary>
public sealed record GetDriversQuery(Guid TenantId) : IQuery<IReadOnlyList<DriverResponse>>;
