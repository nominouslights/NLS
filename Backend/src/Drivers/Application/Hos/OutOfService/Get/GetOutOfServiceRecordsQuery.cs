using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.OutOfService.Get;

/// <summary>Every out-of-service declaration ever recorded for the driver, newest first.</summary>
public sealed record GetOutOfServiceRecordsQuery(Guid TenantId, Guid DriverId) : IQuery<IReadOnlyList<OutOfServiceRecordResponse>>;
