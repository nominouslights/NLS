using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance.Events;

/// <summary>
/// Raised when a vehicle is put on a maintenance plan — both on first assignment and on
/// reassignment to a different plan. Internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record PlanAssignedDomainEvent(Guid AssignmentId, Guid VehicleId, Guid PlanId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
