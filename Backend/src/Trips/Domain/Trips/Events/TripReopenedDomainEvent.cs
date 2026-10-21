using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a dispatcher reopens a trip to undo a mistake (<see cref="Trip.Reopen"/>). The
/// whole record is what the event journal stores, so it is also the audit trail: the activity
/// timeline reads <see cref="Reason"/> straight out of the journaled payload, and
/// <see cref="ClearedReason"/> keeps the cancel / close-without-billing reason the reopen wiped
/// off the trip, so neither is lost. Maps to <c>trips.trip-reopened</c> — Billing drops the
/// trip from its uninvoiced pool when it was reopened out of ReadyForBilling.
/// </summary>
public sealed record TripReopenedDomainEvent(
    Guid TripId,
    TripStatus PreviousStatus,
    TripStatus NewStatus,
    string Reason,
    string? ClearedReason) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
