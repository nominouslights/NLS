using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.Update;

/// <summary>
/// Full-row edit of a shipment's descriptive detail. Routing, handover, and the billing-driven
/// statuses move through their own commands; the shipment number never changes.
/// </summary>
public sealed record UpdateShipmentCommand(
    Guid ShipmentId,
    ShipmentDetails Details,
    ShipmentSource Source,
    string? EnteredBy) : ICommand;
