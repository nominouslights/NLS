using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders.Events;

/// <summary>
/// Raised when a work order is created. Every aggregate write must raise an event — the
/// projection worker polls <c>event_journal</c>, and an eventless write produces no
/// journal row, leaving the read model silently stale. Stays internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record WorkOrderCreatedDomainEvent(Guid WorkOrderId, Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
