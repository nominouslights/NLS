using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Billing;

/// <summary>
/// Published whenever a worksheet's billing state or its set of claimed trips changes —
/// routing key <c>billing.invoice-billing-state-changed</c>. Billing's first public event.
/// <para>
/// It began as pure visibility (so a dispatcher could see a trip was already spoken for) and is
/// now load-bearing: Trips drives the claimed trips' own status from it — Invoiced, Completed on
/// payment, WrittenOff — so a stale or out-of-order delivery can un-complete a paid trip. The
/// consumer carries a per-trip high-water mark for exactly that reason.
/// </para>
/// <para>
/// <see cref="TripIds"/> is the <em>complete current</em> claim set for this invoice, never a
/// delta. Consumers reconcile: every listed trip takes <see cref="State"/>, and any trip they
/// still have pointing at <see cref="InvoiceId"/> that is absent from the list has been
/// released (a line was removed, or the draft was voided) and reverts to unclaimed. Sending
/// the whole set makes the handler idempotent and self-healing under at-least-once delivery —
/// a replayed or out-of-order event converges instead of drifting.
/// </para>
/// <para>
/// <see cref="State"/> travels as a string (<c>OnWorksheet</c> / <c>Invoiced</c> / <c>Paid</c> /
/// <c>Released</c>): integration events never reference another module's internal enums.
/// <see cref="TenantId"/> is in the payload because handlers run outside any HTTP request.
/// </para>
/// </summary>
public sealed record InvoiceBillingStateChangedIntegrationEvent(
    Guid InvoiceId,
    Guid TenantId,
    string InvoiceNumber,
    string State,
    IReadOnlyList<Guid> TripIds,
    string? QboInvoiceId,
    DateOnly? QboEnteredDate,
    DateOnly? PaymentConfirmedDate,
    // Why the invoice was written off — carried so the trips can record the same reason.
    // Null in every other state; older outbox payloads lack the field and deserialize null.
    string? WrittenOffReason = null) : IntegrationEvent;

/// <summary>
/// The wire values for <see cref="InvoiceBillingStateChangedIntegrationEvent.State"/>. Shared
/// so producer and consumer cannot drift on spelling — this is contract, not an enum leak.
/// </summary>
public static class TripBillingStates
{
    /// <summary>Claimed by a draft worksheet — spoken for, but not yet billed.</summary>
    public const string OnWorksheet = "OnWorksheet";

    /// <summary>The worksheet has been keyed into QuickBooks Online.</summary>
    public const string Invoiced = "Invoiced";

    /// <summary>Payment against the QBO invoice has been confirmed by hand.</summary>
    public const string Paid = "Paid";

    /// <summary>
    /// The invoice will never be collected. The trips stay claimed — a written-off trip must
    /// never drift back into the billable pool and get invoiced a second time — which is what
    /// separates this from <see cref="Released"/>.
    /// </summary>
    public const string WrittenOff = "WrittenOff";

    /// <summary>The claim is gone (line removed, or the draft voided) — the trip is billable again.</summary>
    public const string Released = "Released";
}
