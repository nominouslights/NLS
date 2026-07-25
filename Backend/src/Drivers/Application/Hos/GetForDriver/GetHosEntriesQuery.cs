using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.GetForDriver;

/// <summary>Lists a driver's HOS log entries, newest date first.</summary>
public sealed record GetHosEntriesQuery(Guid TenantId, Guid DriverId)
    : IQuery<IReadOnlyList<HosEntryResponse>>;
