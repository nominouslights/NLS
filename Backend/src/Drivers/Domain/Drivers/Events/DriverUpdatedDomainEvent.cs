using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers.Events;

/// <summary>Raised when a driver's registration details change (name, licence, source, …).</summary>
public sealed record DriverUpdatedDomainEvent(Guid DriverId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
