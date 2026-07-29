using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.UnpairRoundTrip;

/// <summary>
/// Operational undo of a round-trip pairing (POST /api/trips/{id}/unpair-round-trip):
/// clears RoundTripKey and Direction on BOTH legs of the pair. Fails when the trip is
/// not paired.
/// </summary>
public sealed record UnpairRoundTripCommand(Guid TripId) : ICommand;
