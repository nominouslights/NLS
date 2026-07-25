using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers.Events;

/// <summary>Raised on every status transition through the driver lifecycle matrix.</summary>
public sealed record DriverStatusChangedDomainEvent(
    Guid DriverId,
    DriverStatus PreviousStatus,
    DriverStatus NewStatus) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
