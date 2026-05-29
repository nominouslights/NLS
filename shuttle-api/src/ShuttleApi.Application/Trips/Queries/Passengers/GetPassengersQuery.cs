using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record GetPassengersQuery(Guid TripId) : IQuery<IReadOnlyList<PassengerResult>>;
