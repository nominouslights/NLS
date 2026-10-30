using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Status;

/// <summary>The driver's standing right now: the engine's evaluation over the last 21 days plus today.</summary>
public sealed record GetHosStatusQuery(Guid TenantId, Guid DriverId) : IQuery<HosStatusResponse>;
