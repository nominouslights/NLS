using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.RemoveLeg;

/// <summary>Drops a planned leg from a shipment's route and closes the numbering gap.</summary>
public sealed record RemoveShipmentLegCommand(Guid ShipmentId, int Sequence) : ICommand;
