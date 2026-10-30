using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.ConvertToPassengerTrip;

/// <summary>
/// Turns a Scheduled deadhead back into an ordinary trip (<c>Trip.ConvertToPassengerTrip</c>).
/// Only the flag changes; the start and finish gates apply again by themselves.
/// </summary>
public sealed record ConvertTripToPassengerTripCommand(Guid TripId) : ICommand;
