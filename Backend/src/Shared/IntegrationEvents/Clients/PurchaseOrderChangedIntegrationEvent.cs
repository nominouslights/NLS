using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Clients;

/// <summary>
/// Published whenever a client purchase order is created or amended — routing key
/// <c>clients.purchase-order-changed</c>. Carries the full billing-relevant snapshot so
/// Billing can maintain its <c>purchase_order_snapshots</c> replica by upserting on
/// <see cref="PurchaseOrderId"/> (idempotent under at-least-once delivery) and never needs a
/// library reference to Clients.
/// <para>
/// A PO is negotiated separately from the contract, so it carries its own terms:
/// <see cref="RoundTripRateCad"/> and <see cref="OneWayRateCad"/> override the contract's
/// rate per round trip for work booked against this PO. Either may be null, meaning "no PO
/// term" — the consumer falls back to the contract rate, and a one-way leg with no explicit
/// one-way rate falls back to half the effective round-trip rate.
/// <see cref="AmountCad"/> and <see cref="Expiry"/> are advisory: exceeding them warns, it
/// never blocks pricing.
/// </para>
/// Every money figure here is <b>tax-inclusive</b> and no tax flag travels on this event:
/// the platform computes no GST/HST/PST at all — QuickBooks Online owns tax calculation.
/// <see cref="TenantId"/> is part of the payload because handlers run outside any HTTP
/// request.
/// </summary>
public sealed record PurchaseOrderChangedIntegrationEvent(
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid ClientId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    decimal? RoundTripRateCad,
    decimal? OneWayRateCad) : IntegrationEvent;
