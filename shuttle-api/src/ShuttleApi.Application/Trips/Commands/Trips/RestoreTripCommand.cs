using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record RestoreTripCommand(Guid TripId) : ICommand;
