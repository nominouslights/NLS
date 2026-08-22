using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Domain.Invoices.Events;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// Payment confirmation is a manual reconciliation, exactly like the QBO reference: the
/// platform records that money arrived so dispatch can answer outstanding-vs-paid without
/// opening QuickBooks. These pin the lifecycle guards around it.
/// </summary>
public class InvoicePaymentConfirmationTests
{
    private static readonly DateOnly Entered = new(2026, 8, 12);
    private static readonly DateOnly Received = new(2026, 8, 30);

    private static Invoice Draft()
    {
        var line = InvoiceLine.Create("Round trip", [Guid.NewGuid()], "TR-1", new DateOnly(2026, 8, 1), 1, 1200m);
        Assert.True(line.IsSuccess);

        var invoice = Invoice.CreateDraft(
            Guid.NewGuid(), "INV-2026-114", Guid.NewGuid(), "Alamos", null, null, null,
            30, gstApplicable: true, Invoice.StandardGstRate,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), [line.Value]);

        Assert.True(invoice.IsSuccess);
        return invoice.Value;
    }

    private static Invoice Entered_Invoice()
    {
        var invoice = Draft();
        Assert.True(invoice.MarkEnteredInQbo("QBO-8871", Entered).IsSuccess);
        return invoice;
    }

    [Fact]
    public void A_new_draft_is_outstanding()
    {
        Assert.Null(Draft().PaymentConfirmedDate);
    }

    [Fact]
    public void Payment_cannot_be_confirmed_before_the_worksheet_is_entered_in_qbo()
    {
        var invoice = Draft();

        var result = invoice.ConfirmPayment(Received);

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.NotEnteredForPayment, result.Error);
        Assert.Null(invoice.PaymentConfirmedDate);
    }

    [Fact]
    public void Confirming_payment_moves_an_entered_worksheet_to_paid()
    {
        var invoice = Entered_Invoice();

        var result = invoice.ConfirmPayment(Received);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(Received, invoice.PaymentConfirmedDate);
        Assert.Contains(invoice.DomainEvents, e => e is InvoicePaymentConfirmedDomainEvent { ConfirmedDate: not null });
    }

    [Fact]
    public void Confirming_payment_leaves_the_qbo_reference_intact()
    {
        var invoice = Entered_Invoice();

        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);

        Assert.Equal("QBO-8871", invoice.QboInvoiceId);
        Assert.Equal(Entered, invoice.QboEnteredDate);
    }

    [Fact]
    public void Payment_cannot_be_confirmed_twice()
    {
        var invoice = Entered_Invoice();
        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);

        var result = invoice.ConfirmPayment(Received);

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.NotEnteredForPayment, result.Error);
    }

    [Fact]
    public void Clearing_a_mistaken_confirmation_returns_the_worksheet_to_entered()
    {
        var invoice = Entered_Invoice();
        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);

        var result = invoice.ClearPaymentConfirmation();

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.EnteredInQbo, invoice.Status);
        Assert.Null(invoice.PaymentConfirmedDate);
        Assert.Equal("QBO-8871", invoice.QboInvoiceId);
    }

    [Fact]
    public void Clearing_a_confirmation_that_was_never_made_fails()
    {
        var result = Entered_Invoice().ClearPaymentConfirmation();

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.NotPaid, result.Error);
    }

    [Fact]
    public void A_paid_worksheet_refuses_a_write_off_until_the_payment_is_cleared()
    {
        var invoice = Entered_Invoice();
        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);

        var blocked = invoice.WriteOff(invoice.TotalCad, Received, "reason");

        // A settled invoice has nothing to write off; clearing the payment first keeps the
        // recorded settlement from silently vanishing.
        Assert.True(blocked.IsFailure);
        Assert.Equal(InvoiceErrors.NotEnteredForWriteOff, blocked.Error);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);

        Assert.True(invoice.ClearPaymentConfirmation().IsSuccess);
        Assert.True(invoice.WriteOff(invoice.TotalCad, Received, "reason").IsSuccess);
        Assert.Equal(InvoiceStatus.WrittenOff, invoice.Status);
    }

    [Fact]
    public void The_qbo_reference_can_still_be_corrected_while_paid()
    {
        var invoice = Entered_Invoice();
        Assert.True(invoice.ConfirmPayment(Received).IsSuccess);

        // Fixing a mistyped QBO number must not cost the recorded payment.
        var result = invoice.UpdateQboReference("QBO-8872", Entered);

        Assert.True(result.IsSuccess);
        Assert.Equal("QBO-8872", invoice.QboInvoiceId);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(Received, invoice.PaymentConfirmedDate);
    }
}
