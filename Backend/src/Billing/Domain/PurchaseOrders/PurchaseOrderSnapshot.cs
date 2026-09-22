using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.PurchaseOrders;

/// <summary>
/// Billing's replica of a client purchase order, maintained by upserting
/// <c>PurchaseOrderChangedIntegrationEvent</c> payloads keyed on <see cref="Id"/> (the
/// Clients module's PurchaseOrderId) and deleting on
/// <c>PurchaseOrderDeletedIntegrationEvent</c> — never by querying Clients. A plain replica
/// row, not an aggregate: Clients owns the PO; Billing only reads it to price draft
/// invoices.
/// <para>
/// Each PO is negotiated separately, so it carries its own terms.
/// <see cref="RoundTripRateCad"/> overrides the contract's rate per round trip for work
/// booked against this PO; <see cref="OneWayRateCad"/> is an absolute one-way figure, not a
/// fraction. Either being null means "no PO term" and the fallback applies (contract rate,
/// and half the effective round-trip rate respectively). Both are <b>tax-inclusive</b>: the
/// platform computes no GST/HST/PST anywhere — QuickBooks Online owns tax.
/// </para>
/// <see cref="Expiry"/> and <see cref="AmountCad"/> are advisory only. A leg outside the PO
/// window, or a draft that pushes the PO past its value, is <em>flagged</em> — pricing and
/// invoicing are never refused over either.
/// </summary>
public sealed class PurchaseOrderSnapshot : ITenantScoped
{
    /// <summary>The Clients module's PurchaseOrderId — the upsert key.</summary>
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }

    /// <summary>The PO number as typed by dispatch — the join key to a trip's PoNumber.</summary>
    public string PoNumber { get; set; } = null!;

    public DateOnly Issued { get; set; }
    public DateOnly? Expiry { get; set; }

    /// <summary>The authorized value, advisory. Null means no cap was recorded.</summary>
    public decimal? AmountCad { get; set; }

    /// <summary>This PO's negotiated round-trip rate; null falls back to the contract rate.</summary>
    public decimal? RoundTripRateCad { get; set; }

    /// <summary>This PO's negotiated one-way rate; null falls back to half the round-trip rate.</summary>
    public decimal? OneWayRateCad { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Whether the PO's own window covers the given service date. A null
    /// <see cref="Expiry"/> is open-ended. Used to flag a leg, never to reject it.
    /// </summary>
    public bool CoversDate(DateOnly serviceDate) =>
        serviceDate >= Issued && (Expiry is null || serviceDate <= Expiry);
}
