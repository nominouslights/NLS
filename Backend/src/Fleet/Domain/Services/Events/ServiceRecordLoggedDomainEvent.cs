using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Services.Events;

/// <summary>
/// Raised when a maintenance service record is logged. Every aggregate write must raise
/// an event — an eventless write produces no journal row and the read model silently
/// goes stale. Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record ServiceRecordLoggedDomainEvent(Guid ServiceRecordId, Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
