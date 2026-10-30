using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Manifests.Events;

/// <summary>
/// Raised when a manifest's copied route name follows its trip onto a new route
/// (<see cref="TripManifest.RenameRoute"/>). Carries no Source/EnteredBy on purpose: nobody
/// edited the manifest, so the activity timeline must not attribute this to a person.
/// Internal only.
/// </summary>
public sealed record TripManifestRouteRenamedDomainEvent(
    Guid ManifestId,
    string PreviousRoute,
    string Route) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
