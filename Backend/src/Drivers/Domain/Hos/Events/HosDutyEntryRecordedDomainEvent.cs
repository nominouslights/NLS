using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised whenever an HOS entry is recorded (manual paper backup or driver app). Unlike
/// the credential/clearance aggregates — which raise no event — HOS must emit one so the
/// projection worker (which polls <c>event_journal</c>) upserts the read model
/// incrementally; an eventless insert would only appear after a full rebuild. Stays
/// internal to the module: <c>DriversIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record HosDutyEntryRecordedDomainEvent(Guid EntryId, Guid DriverId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
