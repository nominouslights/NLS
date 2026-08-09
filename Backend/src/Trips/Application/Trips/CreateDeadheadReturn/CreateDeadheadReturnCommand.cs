using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;

/// <summary>
/// Creates the empty repositioning ("deadhead") return leg for a client trip with no
/// return (POST /api/trips/{id}/deadhead-return): a NEW trip on the reversed corridor,
/// IsEmptyLeg, Inbound, sharing a fresh "merge:" round-trip key with the source trip
/// (which becomes Outbound). Someone drives the unit back, so the leg always carries a
/// driver and vehicle: <see cref="DriverId"/>/<see cref="VehicleId"/> override the source
/// trip's assignment (validated against the lookups); left null, the source's own
/// driver/vehicle carry over — and the command is refused if that leaves either missing.
/// Returns the new trip's id.
/// </summary>
public sealed record CreateDeadheadReturnCommand(
    Guid TripId,
    Guid? DriverId,
    Guid? VehicleId) : ICommand<Guid>;
