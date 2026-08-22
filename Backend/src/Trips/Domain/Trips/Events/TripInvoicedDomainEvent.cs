using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when the worksheet carrying this trip is keyed into QuickBooks Online, or when a
/// payment confirmation is cleared and the trip drops back out of Completed. Internal to Trips:
/// it journals the transition and gives the Dispatcher activity timeline a readable
/// <c>trip-invoiced</c> entry, but publishes nothing — Billing is where this fact originated.
/// </summary>
public sealed record TripInvoicedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
