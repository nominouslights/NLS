using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips.Events;

public sealed record TripCompletedEvent(Guid TripId) : DomainEvent;
