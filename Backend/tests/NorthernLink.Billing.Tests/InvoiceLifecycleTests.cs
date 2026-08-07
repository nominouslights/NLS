using NorthernLink.Billing.Domain.Invoices;
using Xunit;

namespace NorthernLink.Billing.Tests;

public class InvoiceLifecycleTests
{
    [Fact]
    public void CreateDraft_starts_in_draft_with_computed_totals()
    {
        var invoice = TestBilling.DraftInvoice(
            gstApplicable: true,
            TestBilling.Line(1m, 120m),
            TestBilling.Line(0.5m, 120m));

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Null(invoice.QboInvoiceId);
        Assert.Null(invoice.QboEnteredDate);
        Assert.Equal(180m, invoice.SubtotalCad);
        Assert.Equal(9m, invoice.GstCad);
        Assert.Equal(189m, invoice.TotalCad);
    }

    [Fact]
    public void CreateDraft_rejects_inverted_period()
    {
        var result = Invoice.CreateDraft(
            TestBilling.TenantId, "INV-0001", TestBilling.ClientId, "Client",
            null, null, null, 30, true, Invoice.StandardGstRate,
            new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 1), []);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.InvalidPeriod", result.Error.Code);
    }

    [Fact]
    public void ReplaceLines_works_only_while_draft()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var replace = invoice.ReplaceLines([TestBilling.Line(1m, 150m)]);
        Assert.True(replace.IsSuccess);
        Assert.Equal(150m, invoice.SubtotalCad);

        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);

        var afterEntered = invoice.ReplaceLines([TestBilling.Line(1m, 99m)]);
        Assert.True(afterEntered.IsFailure);
        Assert.Equal("Billing.Invoice.NotDraft", afterEntered.Error.Code);
        Assert.Equal(150m, invoice.SubtotalCad);
    }

    [Fact]
    public void MarkEnteredInQbo_transitions_draft_to_entered_once()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var entered = invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1));
        Assert.True(entered.IsSuccess);
        Assert.Equal(InvoiceStatus.EnteredInQbo, invoice.Status);
        Assert.Equal("QBO-1042", invoice.QboInvoiceId);
        Assert.Equal(new DateOnly(2026, 8, 1), invoice.QboEnteredDate);

        var again = invoice.MarkEnteredInQbo("QBO-1043", new DateOnly(2026, 8, 2));
        Assert.True(again.IsFailure);
        Assert.Equal("Billing.Invoice.AlreadyEntered", again.Error.Code);
    }

    [Fact]
    public void MarkEnteredInQbo_requires_a_qbo_invoice_number()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var result = invoice.MarkEnteredInQbo("  ", new DateOnly(2026, 8, 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.QboInvoiceIdRequired", result.Error.Code);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }

    [Fact]
    public void MarkEnteredInQbo_is_rejected_from_void()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.Void().IsSuccess);

        var result = invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.AlreadyEntered", result.Error.Code);
        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.Null(invoice.QboInvoiceId);
    }

    [Fact]
    public void UpdateQboReference_requires_entered_status()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var early = invoice.UpdateQboReference("QBO-1", new DateOnly(2026, 8, 1));
        Assert.True(early.IsFailure);
        Assert.Equal("Billing.Invoice.NotEntered", early.Error.Code);

        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);
        var update = invoice.UpdateQboReference("QBO-9999", new DateOnly(2026, 8, 5));
        Assert.True(update.IsSuccess);
        Assert.Equal("QBO-9999", invoice.QboInvoiceId);
        Assert.Equal(new DateOnly(2026, 8, 5), invoice.QboEnteredDate);
    }

    [Fact]
    public void UpdateQboReference_is_rejected_from_void()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.Void().IsSuccess);

        var result = invoice.UpdateQboReference("QBO-1", new DateOnly(2026, 8, 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.NotEntered", result.Error.Code);
    }

    [Fact]
    public void UpdateQboReference_requires_a_qbo_invoice_number()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);

        var result = invoice.UpdateQboReference("   ", new DateOnly(2026, 8, 5));

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.QboInvoiceIdRequired", result.Error.Code);
        // The prior reference is left untouched on a rejected correction.
        Assert.Equal("QBO-1042", invoice.QboInvoiceId);
        Assert.Equal(new DateOnly(2026, 8, 1), invoice.QboEnteredDate);
    }

    [Fact]
    public void Write_off_zeroes_the_outstanding_balance_and_keeps_the_qbo_reference()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);
        Assert.Equal(invoice.TotalCad, invoice.OutstandingCad);

        var result = invoice.WriteOff(invoice.TotalCad, new DateOnly(2026, 9, 15), "Client insolvent");

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.WrittenOff, invoice.Status);
        Assert.Equal(0m, invoice.OutstandingCad); // "zero the balance" is exactly this
        Assert.Equal(invoice.TotalCad, invoice.WrittenOffAmountCad);
        Assert.Equal(new DateOnly(2026, 9, 15), invoice.WrittenOffDate);
        Assert.Equal("Client insolvent", invoice.WrittenOffReason);
        // The QBO invoice still exists — only its collectability changed.
        Assert.Equal("QBO-1042", invoice.QboInvoiceId);
        Assert.Equal(new DateOnly(2026, 8, 1), invoice.QboEnteredDate);
    }

    [Fact]
    public void Write_off_validates_state_amount_and_reason()
    {
        // Only from EnteredInQbo: a draft was never sent, void it instead.
        var draft = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.Equal(InvoiceErrors.NotEnteredForWriteOff, draft.WriteOff(10m, new DateOnly(2026, 9, 15), "x").Error);

        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);

        Assert.Equal(InvoiceErrors.InvalidWriteOffAmount, invoice.WriteOff(0m, new DateOnly(2026, 9, 15), "x").Error);
        Assert.Equal(InvoiceErrors.WriteOffExceedsTotal, invoice.WriteOff(invoice.TotalCad + 0.01m, new DateOnly(2026, 9, 15), "x").Error);
        Assert.Equal(InvoiceErrors.WriteOffReasonRequired, invoice.WriteOff(invoice.TotalCad, new DateOnly(2026, 9, 15), "  ").Error);
        Assert.Equal(InvoiceStatus.EnteredInQbo, invoice.Status); // untouched by the rejections
    }

    [Fact]
    public void A_written_off_invoice_is_terminal()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);
        Assert.True(invoice.WriteOff(invoice.TotalCad, new DateOnly(2026, 9, 15), "Client insolvent").IsSuccess);

        Assert.True(invoice.MarkEnteredInQbo("QBO-2000", new DateOnly(2026, 9, 16)).IsFailure);
        Assert.True(invoice.ReplaceLines([TestBilling.Line()]).IsFailure);
        Assert.True(invoice.ConfirmPayment(new DateOnly(2026, 9, 16)).IsFailure);
        Assert.True(invoice.ClearPaymentConfirmation().IsFailure);
        Assert.True(invoice.Void().IsFailure);
        Assert.True(invoice.UpdateQboReference("QBO-2000", new DateOnly(2026, 9, 16)).IsFailure);
        Assert.True(invoice.WriteOff(invoice.TotalCad, new DateOnly(2026, 9, 17), "again").IsFailure);
        Assert.Equal(InvoiceStatus.WrittenOff, invoice.Status);
    }

    [Fact]
    public void Void_works_only_from_draft()
    {
        var draft = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(draft.Void().IsSuccess);
        Assert.Equal(InvoiceStatus.Void, draft.Status);

        var entered = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(entered.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);
        var voidEntered = entered.Void();
        Assert.True(voidEntered.IsFailure);
        Assert.Equal("Billing.Invoice.NotDraft", voidEntered.Error.Code);
    }

    [Fact]
    public void Void_is_rejected_when_already_void()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.Void().IsSuccess);

        var again = invoice.Void();

        Assert.True(again.IsFailure);
        Assert.Equal("Billing.Invoice.NotDraft", again.Error.Code);
    }

    [Fact]
    public void Entry_into_quickbooks_is_a_one_way_door()
    {
        // Reopen (EnteredInQbo -> Draft) no longer exists: once the worksheet is keyed into
        // QBO the invoice is out in the world — correct the reference or write it off, never
        // un-send it. This pins the method's absence semantically: re-entering must fail.
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.MarkEnteredInQbo("QBO-1042", new DateOnly(2026, 8, 1)).IsSuccess);

        var reEntered = invoice.MarkEnteredInQbo("QBO-2000", new DateOnly(2026, 9, 1));

        Assert.True(reEntered.IsFailure);
        Assert.Equal(InvoiceStatus.EnteredInQbo, invoice.Status);
        Assert.Equal("QBO-1042", invoice.QboInvoiceId); // the original entry stands
    }

    [Fact]
    public void CreateDraft_computes_totals_without_gst_when_not_applicable()
    {
        var invoice = TestBilling.DraftInvoice(
            gstApplicable: false,
            TestBilling.Line(1m, 120m),
            TestBilling.Line(2m, 100m),
            TestBilling.Line(0.5m, 120m));

        Assert.Equal(380m, invoice.SubtotalCad);
        Assert.Equal(0m, invoice.GstCad);
        Assert.Equal(380m, invoice.TotalCad);
    }

    [Fact]
    public void InvoiceLine_computes_amount_and_validates_inputs()
    {
        var line = InvoiceLine.Create("Half trip", [], "TR-1", new DateOnly(2026, 7, 6), 0.5m, 121.99m).Value;
        Assert.Equal(61m, line.AmountCad); // 60.995 rounds to 61.00 (banker's rounding of midpoint).

        Assert.Equal(
            "Billing.Invoice.InvalidLineDescription",
            InvoiceLine.Create("  ", [], null, null, 1m, 10m).Error.Code);
        Assert.Equal(
            "Billing.Invoice.InvalidLineQuantity",
            InvoiceLine.Create("x", [], null, null, 0m, 10m).Error.Code);
        Assert.Equal(
            "Billing.Invoice.InvalidLineUnitPrice",
            InvoiceLine.Create("x", [], null, null, 1m, -1m).Error.Code);
    }
}
