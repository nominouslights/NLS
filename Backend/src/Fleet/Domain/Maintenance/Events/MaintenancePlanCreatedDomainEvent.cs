using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance.Events;

/// <summary>
/// Raised when a maintenance plan is created. Every aggregate write must raise an event —
/// an eventless write produces no journal row and the read model silently goes stale.
/// Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record MaintenancePlanCreatedDomainEvent(Guid PlanId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
