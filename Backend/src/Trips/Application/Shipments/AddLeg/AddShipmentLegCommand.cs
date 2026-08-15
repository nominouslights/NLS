using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.AddLeg;

/// <summary>
/// Routes a shipment through another trip, appended after any legs already planned. Two legs is a
/// hub transfer — out on one run, onward on the next — still one billable item.
/// <para>
/// Takes only the trip and the corridor. It carries no client field <b>by design</b>: the trip's
/// client and the shipment's are unrelated facts, and a command that could carry both is a
/// command someone will eventually use to copy one onto the other.
/// </para>
/// </summary>
public sealed record AddShipmentLegCommand(
    Guid ShipmentId,
    Guid TripId,
    Guid? FromStopId,
    string? FromName,
    Guid? ToStopId,
    string? ToName) : ICommand;
