using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Shipments.SetBilling;

/// <summary>
/// Attaches (or clears) the party who pays and what they pay.
/// <para>
/// This is the route every backfilled legacy cargo row travels: the old manifest jsonb never
/// recorded a client, so those rows land clientless and unbillable until a dispatcher attributes
/// them here. The alternative — copying the carrying trip's client during the migration — is
/// exactly the mis-billing this whole feature exists to prevent.
/// </para>
/// </summary>
public sealed record SetShipmentBillingCommand(
    Guid ShipmentId,
    Guid? ClientId,
    string? PoNumber,
    decimal? ChargeCad,
    ShipmentPaymentMethod? PaymentMethod) : ICommand;
