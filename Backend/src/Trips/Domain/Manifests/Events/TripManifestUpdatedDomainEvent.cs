using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Manifests.Events;

/// <summary>
/// Raised on every edit to a manifest (passengers, cargo, or §1 trip info). Carries the
/// provenance of the edit — <see cref="Source"/> (App or Dispatcher) and
/// <see cref="EnteredBy"/> — so the journaled event stream IS the per-manifest audit log
/// ("Driver App edited passengers", "Dispatcher edited cargo", …). One event per mutating
/// call; the audit pipeline requires the raise.
/// </summary>
public sealed record TripManifestUpdatedDomainEvent(
    Guid ManifestId,
    ManifestSource Source,
    string? EnteredBy) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
