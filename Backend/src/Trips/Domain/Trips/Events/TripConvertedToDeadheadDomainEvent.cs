using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a dispatcher turns a Scheduled trip nobody is booked on into a deadhead (an empty
/// repositioning run) — <see cref="Trip.ConvertToDeadhead"/>. <see cref="RemovedManifestIds"/> are
/// the empty manifests deleted in the same save (linked by id or by trip number), so the activity
/// timeline can say what went. Internal only: Billing learns a leg is empty from
/// <c>IsEmptyLeg</c> on the ready-for-billing / completed feed once the run is over, and no
/// other module tracks a trip's passenger status while it is still Scheduled.
/// </summary>
public sealed record TripConvertedToDeadheadDomainEvent(
    Guid TripId,
    IReadOnlyList<Guid> RemovedManifestIds) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
