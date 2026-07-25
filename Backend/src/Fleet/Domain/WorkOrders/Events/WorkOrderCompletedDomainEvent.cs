using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders.Events;

/// <summary>
/// Raised when a work order is closed by the service record that resolved it.
/// Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record WorkOrderCompletedDomainEvent(Guid WorkOrderId, Guid ResolvingServiceId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
