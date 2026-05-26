using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record DeleteTripCommand(Guid TripId) : ICommand;
