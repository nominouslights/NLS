namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// Lifecycle of a trip. The arc is operational up to the point the bus stops, then
/// billing-driven: a client trip finishing its run lands in <see cref="ReadyForBilling"/>
/// rather than <see cref="Completed"/>, and only reaches <see cref="Completed"/> when Billing
/// confirms payment against the QuickBooks invoice. A trip with no client (community runs,
/// walk-up charters — the fare was taken at booking or by the dispatcher) skips the billing
/// arc and completes on operational finish.
/// <para>
/// <see cref="Invoiced"/> and <see cref="WrittenOff"/> are set exclusively by
/// <c>InvoiceBillingStateChangedIntegrationEventHandler</c> reacting to Billing — never by
/// hand. The one exception is <see cref="ReadyForBilling"/> → <see cref="WrittenOff"/>, the
/// dispatcher's escape for a run that will never be invoiced at all (a client with no active
/// contract), which otherwise has no exit.
/// </para>
/// <para>
/// Still deliberately minimal: "open — needs coverage" (Scheduled with no driver) and
/// "empty leg" (<c>Trip.IsEmptyLeg</c>) remain frontend derivations, never persisted statuses.
/// Ordinals are explicit and the original four keep their values, because the <c>?status=</c>
/// query filter has historically accepted numbers.
/// </para>
/// </summary>
public enum TripStatus
{
    Scheduled = 0,
    InProgress = 1,

    /// <summary>The run is over and the trip is waiting for a worksheet. Client trips only.</summary>
    ReadyForBilling = 4,

    /// <summary>Its worksheet has been keyed into QuickBooks Online. Set by Billing.</summary>
    Invoiced = 5,

    /// <summary>Paid (client trips) or finished (clientless runs). Reversible to <see cref="Invoiced"/>.</summary>
    Completed = 2,

    Cancelled = 3,

    /// <summary>
    /// The money will never arrive — the invoice was written off, or the run was never billable
    /// and a dispatcher closed it out. Final.
    /// </summary>
    WrittenOff = 6,
}
