using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance.Events;

/// <summary>
/// Raised immediately before a plan assignment is hard-deleted, so the unassignment reaches
/// consumers — the mapper only ever sees real domain events; the synthetic aggregate-deleted
/// journal row (which drives read-model deletion) is not mapped. Internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record PlanUnassignedDomainEvent(Guid AssignmentId, Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
