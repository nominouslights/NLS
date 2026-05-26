using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record DispatchTripCommand(Guid TripId) : ICommand;
