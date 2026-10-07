using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.ConvertToDeadhead;

/// <summary>
/// Turns a Scheduled trip nobody is booked on into a deadhead (an empty repositioning run),
/// deleting its empty manifest(s) in the same save. Every refusal is a specific 409 — see
/// <c>Trip.ConvertToDeadhead</c>; a concurrent edit is <c>Trips.Trip.ChangedConcurrently</c>.
/// </summary>
public sealed record ConvertTripToDeadheadCommand(Guid TripId) : ICommand;
