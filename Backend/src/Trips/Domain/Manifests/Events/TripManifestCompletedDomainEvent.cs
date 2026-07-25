using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Manifests.Events;

/// <summary>
/// Raised when a completed manifest is recorded (a manifest is only ever created whole —
/// the aggregate has no draft state). The publish hook for the
/// <c>trips.trip-manifest-completed</c> integration event Fleet consumes to materialize
/// the pre- and post-trip vehicle inspection records.
/// </summary>
public sealed record TripManifestCompletedDomainEvent(Guid ManifestId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
