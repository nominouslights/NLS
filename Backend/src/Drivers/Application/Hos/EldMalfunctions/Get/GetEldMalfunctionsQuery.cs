using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.EldMalfunctions.Get;

/// <summary>Every ELD malfunction report for the driver, open ones first, then newest first.</summary>
public sealed record GetEldMalfunctionsQuery(Guid TenantId, Guid DriverId) : IQuery<IReadOnlyList<EldMalfunctionResponse>>;
