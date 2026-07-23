using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Clearances.GetForDriver;

/// <summary>Lists a driver's client-site clearances, newest granted first.</summary>
public sealed record GetDriverClearancesQuery(Guid TenantId, Guid DriverId)
    : IQuery<IReadOnlyList<DriverClearanceResponse>>;
