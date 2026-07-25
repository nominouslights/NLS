using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders.Events;

/// <summary>
/// Raised when a work order advances through its (non-completion) status lifecycle.
/// Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record WorkOrderStatusChangedDomainEvent(
    Guid WorkOrderId,
    WorkOrderStatus PreviousStatus,
    WorkOrderStatus NewStatus) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
