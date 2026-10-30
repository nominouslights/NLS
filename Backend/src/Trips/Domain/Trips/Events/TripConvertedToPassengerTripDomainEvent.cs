using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a Scheduled deadhead is turned back into an ordinary trip
/// (<see cref="Trip.ConvertToPassengerTrip"/>). Nothing else changes with it — the trip's
/// en-route gates (a passenger manifest, or a shipment for a cargo run) and its post-trip
/// inspection gate simply apply again. Internal only, like its counterpart.
/// </summary>
public sealed record TripConvertedToPassengerTripDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
