using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance.Events;

/// <summary>
/// Raised when a preventative-maintenance completion is logged. Internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record PmCompletionLoggedDomainEvent(Guid CompletionId, Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
