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
        Assert.Null(invoice.SentAtUtc);
        Assert.Null(invoice.PaidAtUtc);
        Assert.Equal(QboSyncStatus.NotSynced, invoice.QboSyncStatus);
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

        Assert.True(invoice.Send().IsSuccess);

        var afterSend = invoice.ReplaceLines([TestBilling.Line(1m, 99m)]);
        Assert.True(afterSend.IsFailure);
        Assert.Equal("Billing.Invoice.NotDraft", afterSend.Error.Code);
        Assert.Equal(150m, invoice.SubtotalCad);
    }

    [Fact]
    public void Send_transitions_draft_to_sent_once()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var send = invoice.Send();
        Assert.True(send.IsSuccess);
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        Assert.NotNull(invoice.SentAtUtc);

        var again = invoice.Send();
        Assert.True(again.IsFailure);
        Assert.Equal("Billing.Invoice.AlreadySent", again.Error.Code);
    }

    [Fact]
    public void MarkPaid_requires_sent()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());

        var early = invoice.MarkPaid();
        Assert.True(early.IsFailure);
        Assert.Equal("Billing.Invoice.NotSent", early.Error.Code);

        Assert.True(invoice.Send().IsSuccess);
        var paid = invoice.MarkPaid();
        Assert.True(paid.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAtUtc);

        // Terminal: cannot pay twice or void.
        Assert.True(invoice.MarkPaid().IsFailure);
        Assert.True(invoice.Void().IsFailure);
    }

    [Fact]
    public void Void_works_only_from_draft()
    {
        var draft = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(draft.Void().IsSuccess);
        Assert.Equal(InvoiceStatus.Void, draft.Status);

        var sent = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(sent.Send().IsSuccess);
        var voidSent = sent.Void();
        Assert.True(voidSent.IsFailure);
        Assert.Equal("Billing.Invoice.NotDraft", voidSent.Error.Code);
    }

    [Fact]
    public void SetQboStatus_records_reference_and_flag_in_any_status()
    {
        var invoice = TestBilling.DraftInvoice(true, TestBilling.Line());
        Assert.True(invoice.Send().IsSuccess);

        var result = invoice.SetQboStatus("qbo-1042", QboSyncStatus.Matched);

        Assert.True(result.IsSuccess);
        Assert.Equal("qbo-1042", invoice.QboInvoiceId);
        Assert.Equal(QboSyncStatus.Matched, invoice.QboSyncStatus);
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
