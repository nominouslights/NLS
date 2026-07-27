using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Stops.Events;

/// <summary>Raised when a stop is edited or its active flag flips. Internal only — see <see cref="StopCreatedDomainEvent"/>.</summary>
public sealed record StopUpdatedDomainEvent(Guid StopId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
