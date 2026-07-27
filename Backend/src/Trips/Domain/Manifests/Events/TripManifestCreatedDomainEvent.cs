using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Manifests.Events;

/// <summary>
/// Raised when a passenger + cargo manifest is first recorded. Drives the same-module
/// reaction that links the manifest to its trip by trip number (an idempotent no-op when
/// no planned trip exists). It no longer implies the trip is complete — completion is the
/// explicit <c>ChangeStatus → Complete</c> path. Journaled, so it (with <see cref="Source"/>
/// and <see cref="EnteredBy"/> on the aggregate) is the first entry of the manifest's audit
/// trail.
/// </summary>
public sealed record TripManifestCreatedDomainEvent(Guid ManifestId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
