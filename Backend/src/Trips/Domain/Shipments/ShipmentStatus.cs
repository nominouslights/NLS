namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// Lifecycle of a shipment. Like <see cref="Trips.TripStatus"/> the arc is operational up to
/// handover and billing-driven afterwards, but the fork is on the shipment's <em>own</em>
/// client: a shipment with a client and a charge lands in <see cref="ReadyForBilling"/> on
/// delivery, and one without (a counter sale paid at handover, or a goodwill carry) lands in
/// <see cref="Delivered"/> and never enters the billing arc at all.
/// <para>
/// The paid state is <see cref="Settled"/>, deliberately not "Completed": on
/// <see cref="Trips.TripStatus"/> that name already means "paid" and has to apologise for it,
/// whereas here <see cref="Delivered"/> is the operational end. Two different meanings under
/// one name is how the trip lifecycle got confusing; this one starts clean.
/// </para>
/// <para>
/// <see cref="Invoiced"/>, <see cref="Settled"/>, and <see cref="WrittenOff"/> are set
/// exclusively by <c>ShipmentInvoiceBillingStateChangedIntegrationEventHandler</c> reacting to
/// Billing — never by hand. The one exception is
/// <see cref="ReadyForBilling"/> → <see cref="WrittenOff"/>, the dispatcher's escape for
/// freight that will never be invoiced, which otherwise has no exit.
/// </para>
/// <para>
/// Ordinals are explicit and stable, because the <c>?status=</c> query filter is matched
/// against enum <em>names</em> but nothing stops a stored value from being read as a number.
/// </para>
/// </summary>
public enum ShipmentStatus
{
    /// <summary>Pre-registered. No legs planned yet — cargo can exist long before a trip does.</summary>
    Registered = 0,

    /// <summary>At least one planned leg, nothing picked up yet.</summary>
    Assigned = 1,

    /// <summary>
    /// Picked up on some leg and not yet handed over. Covers freight sitting at a hub between
    /// legs — see <c>Shipment.IsAwaitingTransfer</c>, which is the state a dispatcher has to be
    /// able to see or the goods become invisible inventory.
    /// </summary>
    InTransit = 2,

    /// <summary>Handed over, and not billable — no client, or no charge.</summary>
    Delivered = 3,

    /// <summary>Handed over, has a client and a charge. Billing's feed.</summary>
    ReadyForBilling = 4,

    /// <summary>On a worksheet keyed into QuickBooks Online. Set by Billing.</summary>
    Invoiced = 5,

    /// <summary>Payment confirmed. Reversible to <see cref="Invoiced"/> if a confirmation is cleared in error.</summary>
    Settled = 6,

    Cancelled = 7,

    /// <summary>The money will never arrive. Final.</summary>
    WrittenOff = 8,
}
