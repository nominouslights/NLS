using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Domain.Invoices.Events;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// Asserts each Invoice transition raises the right domain event with the right payload —
/// the event stream is what the read-model projector and audit trail consume, so a missing
/// or wrong event is a silent data bug downstream.
/// </summary>
public class InvoiceDomainEventTests
{
    [Fact]
    public void CreateDraft_raises_drafted_event()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var drafted = Assert.Single(invoice.DomainEvents.OfType<InvoiceDraftedDomainEvent>());
        Assert.Equal(invoice.Id, drafted.InvoiceId);
        Assert.Equal(TestBilling.TenantId, drafted.TenantId);
        Assert.Equal("INV-0001", drafted.InvoiceNumber);
        Assert.Equal(TestBilling.ClientId, drafted.ClientId);
    }

    [Fact]
    public void MarkEnteredInQbo_raises_entered_event_with_trimmed_reference()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        invoice.ClearDomainEvents();

        Assert.True(invoice.MarkEnteredInQbo("  QBO-1042  ", new DateOnly(2026, 8, 1)).IsSuccess);

        var entered = Assert.Single(invoice.DomainEvents.OfType<InvoiceEnteredInQboDomainEvent>());
        Assert.Equal(invoice.Id, entered.InvoiceId);
        Assert.Equal("QBO-1042", entered.QboInvoiceId);
        Assert.Equal(new DateOnly(2026, 8, 1), entered.EnteredDate);
    }

    [Fact]
    public void MarkEnteredInQbo_raises_no_event_when_rejected()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        invoice.ClearDomainEvents();

        Assert.True(invoice.MarkEnteredInQbo(" ", new DateOnly(2026, 8, 1)).IsFailure);

        Assert.Empty(invoice.DomainEvents);
    }

    [Fact]
    public void UpdateQboReference_raises_entered_event_with_corrected_reference()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);
        invoice.ClearDomainEvents();

        Assert.True(invoice.UpdateQboReference("QBO-9999", new DateOnly(2026, 8, 5)).IsSuccess);

        var corrected = Assert.Single(invoice.DomainEvents.OfType<InvoiceEnteredInQboDomainEvent>());
        Assert.Equal("QBO-9999", corrected.QboInvoiceId);
        Assert.Equal(new DateOnly(2026, 8, 5), corrected.EnteredDate);
    }

    [Fact]
    public void Write_off_raises_its_own_event_with_amount_date_and_reason()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);
        invoice.ClearDomainEvents();

        Assert.True(invoice.WriteOff(invoice.TotalCad, new DateOnly(2026, 9, 15), "Client insolvent").IsSuccess);

        var writtenOff = Assert.Single(invoice.DomainEvents.OfType<InvoiceWrittenOffDomainEvent>());
        Assert.Equal(invoice.Id, writtenOff.InvoiceId);
        Assert.Equal(invoice.TotalCad, writtenOff.AmountCad);
        Assert.Equal(new DateOnly(2026, 9, 15), writtenOff.WrittenOffDate);
        Assert.Equal("Client insolvent", writtenOff.Reason);
    }

    [Fact]
    public void Void_raises_status_changed_from_draft_to_void()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        invoice.ClearDomainEvents();

        Assert.True(invoice.Void().IsSuccess);

        var changed = Assert.Single(invoice.DomainEvents.OfType<InvoiceStatusChangedDomainEvent>());
        Assert.Equal(InvoiceStatus.Draft, changed.PreviousStatus);
        Assert.Equal(InvoiceStatus.Void, changed.NewStatus);
    }

    [Fact]
    public void ReplaceLines_raises_lines_replaced_event_with_recomputed_totals()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        invoice.ClearDomainEvents();

        Assert.True(invoice.ReplaceLines(
            [TestBilling.Line(1m, 150m), TestBilling.Line(1m, 50m)]).IsSuccess);

        var replaced = Assert.Single(invoice.DomainEvents.OfType<InvoiceLinesReplacedDomainEvent>());
        Assert.Equal(invoice.Id, replaced.InvoiceId);
        Assert.Equal(2, replaced.LineCount);
        Assert.Equal(200m, replaced.SubtotalCad);
        Assert.Equal(210m, replaced.TotalCad); // 200 + 5% GST
    }
}
