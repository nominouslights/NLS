using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a post-trip vehicle inspection is recorded against this trip — the signal
/// that clears the completion gate (<see cref="TripErrors.PostTripInspectionRequired"/>).
/// Set idempotently from Fleet's <c>fleet.vehicle-inspection-recorded</c> integration event,
/// so re-delivery raises nothing on the second pass. Internal to the module:
/// <c>TripsIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record TripPostTripInspectionRecordedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
