using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;

/// <summary>
/// Creates the empty repositioning ("deadhead") return leg for a client trip with no
/// return (POST /api/trips/{id}/deadhead-return): a NEW trip on the reversed corridor,
/// IsEmptyLeg, Inbound, sharing a fresh "merge:" round-trip key with the source trip
/// (which becomes Outbound). Returns the new trip's id.
/// </summary>
public sealed record CreateDeadheadReturnCommand(Guid TripId) : ICommand<Guid>;
