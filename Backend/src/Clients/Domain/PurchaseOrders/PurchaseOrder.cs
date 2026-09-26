using NorthernLink.Clients.Domain.PurchaseOrders.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.PurchaseOrders;

/// <summary>
/// A purchase order a client issued against their account, referenced when invoicing
/// (invoices snapshot the PO number as a string, so hard-deleting a PO never dangles).
/// Client-scoped reference data with no lifecycle of its own: create/update/hard-delete.
/// <para>
/// A PO carries its own <b>pricing terms</b> — each PO is negotiated separately, so
/// <see cref="RoundTripRateCad"/> and <see cref="OneWayRateCad"/> override the client's
/// contract rate for work booked against this PO. Both are optional and independent: a PO
/// may set one, both, or neither, and whatever is unset falls back (round trip → the
/// contract's rate per round trip; one way → half the effective round-trip rate). Rates are
/// quoted <b>tax-inclusive</b> — the platform computes no GST/HST/PST anywhere, QuickBooks
/// owns tax, so no uplift is ever applied to these figures.
/// </para>
/// Create, Update and MarkDeleted raise domain events so the write lands in
/// <c>event_journal</c> for the audit trail and the read-model projection;
/// <c>ClientsIntegrationEventMapper</c> also maps them to the public
/// <c>PurchaseOrderChangedIntegrationEvent</c> / <c>PurchaseOrderDeletedIntegrationEvent</c>
/// so Billing can keep its <c>purchase_order_snapshots</c> replica without ever referencing
/// this library.
/// </summary>
public sealed class PurchaseOrder : AggregateRoot, ITenantScoped
{
    private PurchaseOrder()
    {
        // EF Core materialization only.
        PoNumber = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid ClientId { get; private set; }
    public string PoNumber { get; private set; }
    public DateOnly Issued { get; private set; }
    public DateOnly? Expiry { get; private set; }
    public decimal? AmountCad { get; private set; }

    /// <summary>
    /// This PO's negotiated round-trip rate, tax-inclusive. Null means "no PO term" — work
    /// on this PO prices at the contract's rate per round trip instead.
    /// </summary>
    public decimal? RoundTripRateCad { get; private set; }

    /// <summary>
    /// This PO's negotiated one-way rate, tax-inclusive — an absolute figure, not a fraction.
    /// <para>
    /// <b>Retained for history; no longer priced.</b> Billing charges one full round-trip
    /// rate for every group, paired or not, because a lone leg still deadheads the vehicle
    /// back — so nothing reads this figure when drafting an invoice. It is still recorded,
    /// edited and projected so past negotiations stay legible, and the whole change is
    /// reversible. The column can be dropped once the owner confirms it is not wanted back.
    /// </para>
    /// </summary>
    public decimal? OneWayRateCad { get; private set; }

    public string? Note { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<PurchaseOrder> Create(
        Guid tenantId,
        Guid clientId,
        string poNumber,
        DateOnly issued,
        DateOnly? expiry,
        decimal? amountCad,
        decimal? roundTripRateCad,
        decimal? oneWayRateCad,
        string? note)
    {
        if (Validate(poNumber, issued, expiry, amountCad, roundTripRateCad, oneWayRateCad) is { } error)
        {
            return Result.Failure<PurchaseOrder>(error);
        }

        var now = DateTimeOffset.UtcNow;
        var purchaseOrder = new PurchaseOrder
        {
            TenantId = tenantId,
            ClientId = clientId,
            PoNumber = poNumber.Trim(),
            Issued = issued,
            Expiry = expiry,
            AmountCad = amountCad,
            RoundTripRateCad = roundTripRateCad,
            OneWayRateCad = oneWayRateCad,
            Note = Clean(note),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        purchaseOrder.Raise(new PurchaseOrderCreatedDomainEvent(purchaseOrder.Id, clientId, tenantId));
        return Result.Success(purchaseOrder);
    }

    public Result Update(
        string poNumber,
        DateOnly issued,
        DateOnly? expiry,
        decimal? amountCad,
        decimal? roundTripRateCad,
        decimal? oneWayRateCad,
        string? note)
    {
        if (Validate(poNumber, issued, expiry, amountCad, roundTripRateCad, oneWayRateCad) is { } error)
        {
            return Result.Failure(error);
        }

        PoNumber = poNumber.Trim();
        Issued = issued;
        Expiry = expiry;
        AmountCad = amountCad;
        RoundTripRateCad = roundTripRateCad;
        OneWayRateCad = oneWayRateCad;
        Note = Clean(note);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new PurchaseOrderUpdatedDomainEvent(Id, ClientId, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Raised by the delete handler immediately before the row is removed, so the mapper can
    /// emit the public removal event. A hard delete raises no domain event by itself, and
    /// Billing's replica has to be told to drop its row.
    /// </summary>
    public void MarkDeleted() => Raise(new PurchaseOrderDeletedDomainEvent(Id, ClientId, TenantId));

    private static Error? Validate(
        string poNumber,
        DateOnly issued,
        DateOnly? expiry,
        decimal? amountCad,
        decimal? roundTripRateCad,
        decimal? oneWayRateCad)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return PurchaseOrderErrors.PoNumberRequired;
        }

        if (expiry is { } expiryDate && expiryDate < issued)
        {
            return PurchaseOrderErrors.InvalidExpiry;
        }

        if (amountCad is { } amount && amount <= 0)
        {
            return PurchaseOrderErrors.InvalidAmount;
        }

        // Each rate is independently optional: setting one never obliges the other. Only a
        // present-but-nonsensical figure is rejected.
        if (roundTripRateCad is { } roundTripRate && roundTripRate <= 0)
        {
            return PurchaseOrderErrors.InvalidRoundTripRate;
        }

        if (oneWayRateCad is { } oneWayRate && oneWayRate <= 0)
        {
            return PurchaseOrderErrors.InvalidOneWayRate;
        }

        return null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
