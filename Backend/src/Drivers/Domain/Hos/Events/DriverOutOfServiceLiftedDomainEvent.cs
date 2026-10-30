using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>Raised when an out-of-service declaration is lifted. Maps to <c>drivers.driver-out-of-service-changed</c> (status <c>Lifted</c>).</summary>
public sealed record DriverOutOfServiceLiftedDomainEvent(
    Guid RecordId,
    Guid DriverId,
    Guid TenantId,
    DateTimeOffset LiftedAtUtc) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
