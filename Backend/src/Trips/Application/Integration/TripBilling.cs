namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Trips' replica of a trip's billing state, maintained by reconciling
/// <c>InvoiceBillingStateChangedIntegrationEvent</c>s — never joined from the Billing module
/// (domain libraries never reference each other). A plain keyed row, not an aggregate: nothing
/// in Trips makes a decision from it, it exists so a dispatcher can see that a trip is already
/// on a worksheet, already in QuickBooks, or already paid.
/// <para>
/// It lives in its own table rather than as columns on <c>rm_trips</c> on purpose:
/// <c>TripProjection</c> rewrites every rm_trips column from the Trip aggregate, so billing
/// columns there would be silently wiped the next time the trip was touched.
/// </para>
/// <para>
/// A trip with no row here is simply unclaimed. Releases delete the row rather than storing a
/// "Released" state, so "is this trip billable" stays a single existence check.
/// </para>
/// </summary>
public sealed class TripBilling
{
    public Guid TripId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>The worksheet currently claiming this trip.</summary>
    public Guid InvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    /// <summary>Wire value from <c>TripBillingStates</c> — OnWorksheet, Invoiced, or Paid.</summary>
    public string State { get; set; } = null!;

    public string? QboInvoiceId { get; set; }
    public DateOnly? QboEnteredDate { get; set; }
    public DateOnly? PaymentConfirmedDate { get; set; }

    /// <summary>Why the invoice was written off — null in every other state.</summary>
    public string? WrittenOffReason { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
