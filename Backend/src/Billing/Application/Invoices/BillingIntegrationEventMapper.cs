using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Domain.Invoices.Events;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Billing;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Application.Invoices;

/// <summary>
/// Billing's explicit domain-event → integration-event translation. Its one public contract is
/// <see cref="InvoiceBillingStateChangedIntegrationEvent"/>, consumed by Trips so a dispatcher
/// can see that a trip is already on a worksheet, already in QuickBooks, or already paid.
/// Extending Billing's public surface means adding a case here plus an event record in
/// NorthernLink.Shared/IntegrationEvents/Billing/ — never auto-publishing.
/// <para>
/// Every mapped event publishes the invoice's <em>current</em> state and <em>whole</em> claimed
/// trip set, read off the aggregate rather than inferred from which event fired. That is what
/// makes correcting a QBO reference on a paid invoice publish <c>Paid</c> instead of wrongly
/// demoting it to <c>Invoiced</c>, and what lets the consumer reconcile removals it was never
/// explicitly told about.
/// </para>
/// </summary>
public sealed class BillingIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate)
    {
        if (aggregate is not Invoice invoice)
        {
            return null;
        }

        // Only events that can change either the claim set or the billing state.
        // InvoiceDraftedDomainEvent IS mapped: CreateDraft adds its lines before raising it, and
        // draft generation is the one path that never follows with a lines-replaced event — skip
        // it and a generated draft's OnWorksheet claim would simply never publish, leaving the
        // claimed trips looking unclaimed in Trips until the first manual line edit.
        var relevant = domainEvent
            is InvoiceDraftedDomainEvent
            or InvoiceLinesReplacedDomainEvent
            or InvoiceEnteredInQboDomainEvent
            or InvoicePaymentConfirmedDomainEvent
            or InvoiceWrittenOffDomainEvent
            or InvoiceStatusChangedDomainEvent;

        if (!relevant)
        {
            return null;
        }

        return new InvoiceBillingStateChangedIntegrationEvent(
            invoice.Id,
            invoice.TenantId,
            invoice.InvoiceNumber,
            StateFor(invoice),
            ClaimedTripIds(invoice),
            invoice.QboInvoiceId,
            invoice.QboEnteredDate,
            invoice.PaymentConfirmedDate,
            invoice.WrittenOffReason);
    }

    /// <summary>
    /// Every status spelled out, with no catch-all. This used to end in
    /// <c>_ => TripBillingStates.Released</c>, which would have quietly turned the new WrittenOff
    /// status into a release — deleting the consumer's replica rows and floating settled trips
    /// back into the billable pool. An unmapped status now throws at the point of publication
    /// instead of shipping a wrong claim set.
    /// </summary>
    private static string StateFor(Invoice invoice) => invoice.Status switch
    {
        InvoiceStatus.Draft => TripBillingStates.OnWorksheet,
        InvoiceStatus.EnteredInQbo => TripBillingStates.Invoiced,
        InvoiceStatus.Paid => TripBillingStates.Paid,
        InvoiceStatus.WrittenOff => TripBillingStates.WrittenOff,
        InvoiceStatus.Void => TripBillingStates.Released,
        _ => throw new InvalidOperationException(
            $"Invoice status {invoice.Status} has no billing wire state — add one before publishing it."),
    };

    /// <summary>
    /// The distinct trips this worksheet currently prices. Void is the only status that releases:
    /// it publishes an empty set and the consumer's reconcile clears every trip it still holds.
    /// A write-off deliberately keeps its claims — the work was billed once, and letting those
    /// trips back into the pool would invite a second invoice for the same runs.
    /// </summary>
    private static IReadOnlyList<Guid> ClaimedTripIds(Invoice invoice) =>
        invoice.Status == InvoiceStatus.Void
            ? []
            : invoice.Lines.SelectMany(line => line.TripIds).Distinct().ToList();
}
