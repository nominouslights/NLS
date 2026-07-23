using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a manifest attaches to a trip that was already terminal (so
/// <c>AttachManifest</c> doesn't go through <c>Complete()</c>'s own event). Internal
/// only — see <see cref="TripScheduledDomainEvent"/>.
/// </summary>
public sealed record TripManifestLinkedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
