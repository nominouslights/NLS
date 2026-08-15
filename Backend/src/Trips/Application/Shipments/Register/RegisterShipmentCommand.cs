using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.Register;

/// <summary>
/// Records cargo. Everything but the description is optional — pre-registration happens at the
/// counter long before anyone knows which run will take it, and often before the rate is agreed.
/// The shipment number is server-issued; <see cref="ClientId"/> is the shipment's OWN payer and
/// is never inferred from any trip.
/// </summary>
public sealed record RegisterShipmentCommand(
    Guid TenantId,
    ShipmentDetails Details,
    ShipmentSource Source,
    string? EnteredBy) : ICommand<Guid>;
