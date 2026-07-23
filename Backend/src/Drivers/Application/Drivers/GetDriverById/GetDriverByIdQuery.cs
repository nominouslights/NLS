using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.GetDriverById;

/// <summary>Fetches one driver by id (404 when unknown to the current tenant).</summary>
public sealed record GetDriverByIdQuery(Guid TenantId, Guid DriverId) : IQuery<DriverResponse>;
