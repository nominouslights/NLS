using NorthernLink.Billing.Application.Invoices;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Domain.Invoices.Events;
using NorthernLink.Shared.IntegrationEvents.Billing;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// The mapper is Billing's only public surface. What matters is that it publishes the
/// invoice's CURRENT state and WHOLE claim set rather than inferring either from which event
/// fired — that is what makes the Trips-side reconcile correct.
/// </summary>
public class BillingIntegrationEventMapperTests
{
    private static readonly Guid TripA = Guid.NewGuid();
    private static readonly Guid TripB = Guid.NewGuid();
    private static readonly DateOnly Entered = new(2026, 8, 12);
    private static readonly DateOnly Received = new(2026, 8, 30);

    private readonly BillingIntegrationEventMapper _mapper = new();

    private static Invoice DraftWith(params Guid[] tripIds)
    {
        var lines = tripIds
            .Select(id =>
            {
                var line = InvoiceLine.Create("Round trip", [id], "TR", new DateOnly(2026, 8, 1), 1, 1200m);
                Assert.True(line.IsSuccess);
                return line.Value;
            })
            .ToList();

        var invoice = Invoice.CreateDraft(
            Guid.NewGuid(), "INV-2026-114", Guid.NewGuid(), "Alamos", null, null, null,
            30, gstApplicable: true, Invoice.StandardGstRate,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), lines);

        Assert.True(invoice.IsSuccess);
        return invoice.Value;
    }

    private InvoiceBillingStateChangedIntegrationEvent Map(Invoice invoice, IDomainEvent domainEvent)
    {
        var mapped = _mapper.Map(domainEvent, invoice);
        return Assert.IsType<InvoiceBillingStateChangedIntegrationEvent>(mapped);
    }

    private static InvoiceLinesReplacedDomainEvent LinesReplaced(Invoice invoice) =>
        new(invoice.Id, invoice.Lines.Count, invoice.SubtotalCad, invoice.TotalCad);

    [Fact]
    public void A_draft_publishes_its_trips_as_on_worksheet()
    {
        var invoice = DraftWith(TripA, TripB);

        var published = Map(invoice, LinesReplaced(invoice));

        Assert.Equal(TripBillingStates.OnWorksheet, published.State);
        Assert.Equal([TripA, TripB], published.TripIds);
        Assert.Null(published.QboInvoiceId);
        Assert.Null(published.PaymentConfirmedDate);
    }

    [Fact]
    public void Entering_in_qbo_publishes_invoiced_with_the_reference()
    {
        var invoice = DraftWith(TripA, TripB);
        Assert.True(invoice.MarkEnteredInQbo("QBO-8871", Entered).IsSuccess);

        var published = Map(invoice, new InvoiceEnteredInQboDomainEvent(invoice.Id, "QBO-8871", Entered));

        Assert.Equal(TripBillingStates.Invoiced, published.State);
        Assert.Equal([TripA, TripB], published.TripIds);
        Assert.Equal("QBO-8871", published.QboInvoiceId);
        Assert.Equal(Entered, published.QboEnteredDate);
    }

    [Fact]
    public void Confirming_payment_publishes_paid()
    {
        var invoice = DraftWith(TripA);
        Assert.True(invoice.MarkEnteredInQbo("QBO-8871", Entered).IsSuccess);
        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);

        var published = Map(invoice, new InvoicePaymentConfirmedDomainEvent(invoice.Id, Received));

        Assert.Equal(TripBillingStates.Paid, published.State);
        Assert.Equal(Received, published.PaymentConfirmedDate);
    }

    [Fact]
    public void Correcting_the_qbo_reference_while_paid_does_not_demote_the_state()
    {
        var invoice = DraftWith(TripA);
        Assert.True(invoice.MarkEnteredInQbo("QBO-8871", Entered).IsSuccess);
        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);
        Assert.True(invoice.UpdateQboReference("QBO-8872", Entered).IsSuccess);

        // UpdateQboReference raises InvoiceEnteredInQboDomainEvent. Inferring state from the
        // event type would wrongly publish Invoiced and lose the paid state on every trip.
        var published = Map(invoice, new InvoiceEnteredInQboDomainEvent(invoice.Id, "QBO-8872", Entered));

        Assert.Equal(TripBillingStates.Paid, published.State);
        Assert.Equal("QBO-8872", published.QboInvoiceId);
    }

    [Fact]
    public void Voiding_publishes_released_with_an_empty_claim_set()
    {
        var invoice = DraftWith(TripA, TripB);
        Assert.True(invoice.Void().IsSuccess);

        var published = Map(
            invoice,
            new InvoiceStatusChangedDomainEvent(invoice.Id, InvoiceStatus.Draft, InvoiceStatus.Void));

        Assert.Equal(TripBillingStates.Released, published.State);
        Assert.Empty(published.TripIds);
    }

    [Fact]
    public void Removing_a_line_publishes_the_remaining_claim_set_so_the_dropped_trip_is_released()
    {
        var invoice = DraftWith(TripA, TripB);
        var keep = invoice.Lines.First();
        Assert.True(invoice.ReplaceLines([keep]).IsSuccess);

        var published = Map(invoice, LinesReplaced(invoice));

        // TripB is absent rather than flagged — the consumer reconciles it away.
        Assert.Equal([TripA], published.TripIds);
        Assert.Equal(TripBillingStates.OnWorksheet, published.State);
    }

    [Fact]
    public void A_trip_priced_by_two_lines_is_published_once()
    {
        var invoice = DraftWith(TripA, TripA);

        var published = Map(invoice, LinesReplaced(invoice));

        Assert.Equal([TripA], published.TripIds);
    }

    [Fact]
    public void Drafting_publishes_on_worksheet_with_the_generated_claims()
    {
        // CreateDraft adds its lines BEFORE raising the drafted event, and draft generation
        // never follows with a lines-replaced event — so this mapping is the only thing that
        // tells Trips about a generated draft's claims. Skipping it (as the mapper once did,
        // on the false premise that a draft had no lines yet) left claimed trips looking
        // unclaimed until the first manual line edit.
        var invoice = DraftWith(TripA, TripB);

        var published = Map(
            invoice,
            new InvoiceDraftedDomainEvent(invoice.Id, invoice.TenantId, invoice.InvoiceNumber, invoice.ClientId));

        Assert.Equal(TripBillingStates.OnWorksheet, published.State);
        Assert.Equal([TripA, TripB], published.TripIds);
    }

    [Fact]
    public void A_write_off_publishes_written_off_and_keeps_the_claim_set()
    {
        // The regression this guards: StateFor used to end in `_ => Released`, so an unmapped
        // status published a release — Trips would delete its replica rows and the settled
        // trips would drift back into the billable pool, ready to be invoiced a second time.
        var invoice = DraftWith(TripA, TripB);
        Assert.True(invoice.MarkEnteredInQbo("QBO-8871", Entered).IsSuccess);
        Assert.True(invoice.WriteOff(invoice.TotalCad, new DateOnly(2026, 9, 15), "Client insolvent").IsSuccess);

        var published = Map(
            invoice,
            new InvoiceWrittenOffDomainEvent(invoice.Id, invoice.TotalCad, new DateOnly(2026, 9, 15), "Client insolvent"));

        Assert.Equal(TripBillingStates.WrittenOff, published.State);
        Assert.Equal([TripA, TripB], published.TripIds); // NOT released — never billable again
        Assert.Equal("QBO-8871", published.QboInvoiceId); // the QBO invoice still exists
        Assert.Equal("Client insolvent", published.WrittenOffReason);
    }
}
