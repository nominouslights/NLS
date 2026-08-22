using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.Cancel;

/// <summary>The consignment is off — the goods are not going.</summary>
public sealed record CancelShipmentCommand(Guid ShipmentId, string? Reason) : ICommand;
