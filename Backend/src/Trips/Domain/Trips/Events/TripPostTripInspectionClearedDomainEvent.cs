using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when the post-trip inspection logged against this trip is removed — the inverse of
/// <see cref="TripPostTripInspectionRecordedDomainEvent"/>. Re-arms the completion gate
/// (<see cref="TripErrors.PostTripInspectionRequired"/>) so a trip whose post-trip DVIR was
/// deleted can no longer be completed until a fresh one is logged. Driven by Fleet's
/// <c>fleet.vehicle-inspection-removed</c> integration event, matched by trip number. Set
/// idempotently, so re-delivery raises nothing on the second pass. Internal to the module:
/// <c>TripsIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record TripPostTripInspectionClearedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
