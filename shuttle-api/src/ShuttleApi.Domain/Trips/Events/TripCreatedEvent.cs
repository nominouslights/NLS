using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips.Events;

public sealed record TripCreatedEvent(Guid TripId) : DomainEvent;
