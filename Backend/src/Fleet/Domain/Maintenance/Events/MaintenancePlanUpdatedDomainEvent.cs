using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance.Events;

/// <summary>
/// Raised when a maintenance plan's details, items, or overhauls are replaced. Internal to
/// the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record MaintenancePlanUpdatedDomainEvent(Guid PlanId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
