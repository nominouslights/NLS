using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when the work order generated from this inspection's defects is linked back.
/// Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record VehicleInspectionWorkOrderLinkedDomainEvent(Guid InspectionId, Guid WorkOrderId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
