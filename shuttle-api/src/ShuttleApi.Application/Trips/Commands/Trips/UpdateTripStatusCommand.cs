using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record UpdateTripStatusCommand(Guid TripId, TripStatus Status) : ICommand;
