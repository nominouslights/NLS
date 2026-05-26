using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips.Events;

public sealed record TripDispatchedEvent(Guid TripId, Guid DriverId) : DomainEvent;
