using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips.Events;

public sealed record TripStopAddedEvent(Guid TripId, Guid StopId) : DomainEvent;
