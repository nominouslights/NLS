using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Billing.Domain.Invoices;
using Xunit;

namespace NorthernLink.Billing.Tests;

public class InvoiceDraftBuilderTests
{
    private static readonly DateOnly PeriodStart = new(2026, 7, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 31);

    [Fact]
    public void Complete_round_trip_pair_prices_as_one_line_at_full_rate()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg]);

        Assert.True(result.IsSuccess);
        var draft = result.Value;

        var line = Assert.Single(draft.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(120m, line.UnitPriceCad);
        Assert.Equal(120m, line.AmountCad);
        Assert.StartsWith("Corridor round trip", line.Description);
        Assert.Contains("Thompson–Lynn Lake", line.Description);
        Assert.Equal(new DateOnly(2026, 7, 6), line.ServiceDate);
        Assert.Equal(2, line.TripIds.Count);
        Assert.Contains(outbound.Id, line.TripIds);
        Assert.Contains(returnLeg.Id, line.TripIds);
        Assert.Equal(2, draft.ClaimedTripIds.Count);
        Assert.Contains(outbound.Id, draft.ClaimedTripIds);
        Assert.Contains(returnLeg.Id, draft.ClaimedTripIds);
    }

    [Fact]
    public void Orphan_leg_prices_as_half_line_flagged_for_review()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 8), "rt-lonely", "TR-4830");

        var result = InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [orphan]);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(0.5m, line.Quantity);
        Assert.Equal(120m, line.UnitPriceCad);
        Assert.Equal(60m, line.AmountCad);
        Assert.Contains(InvoiceDraftBuilder.UnpairedLegFlag, line.Description);
        Assert.Equal("TR-4830", line.TripNumber);
        Assert.Equal([orphan.Id], result.Value.ClaimedTripIds);
    }

    [Fact]
    public void Group_pairing_an_outbound_with_an_inbound_prices_as_one_full_line()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var outbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", "TR-01O", direction: "Outbound");
        var inbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", "TR-01R", direction: "Inbound");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, inbound]);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(120m, line.AmountCad);
        Assert.StartsWith("Corridor round trip", line.Description);
        Assert.Equal(2, line.TripIds.Count);
        Assert.Equal(2, result.Value.ClaimedTripIds.Count);
    }

    [Fact]
    public void Two_legs_sharing_a_direction_are_not_a_pair_and_bill_as_two_half_lines()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // Two Outbound legs on the same key — a data anomaly, never a valid round trip.
        var a = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-dup", "TR-A", direction: "Outbound",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero));
        var b = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-dup", "TR-B", direction: "Outbound",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero));

        var result = InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [a, b]);

        Assert.True(result.IsSuccess);
        var draft = result.Value;
        Assert.Equal(2, draft.Lines.Count);
        Assert.All(draft.Lines, l => Assert.Equal(0.5m, l.Quantity));
        Assert.All(draft.Lines, l => Assert.Contains(InvoiceDraftBuilder.UnpairedLegFlag, l.Description));
        Assert.All(draft.Lines, l => Assert.Single(l.TripIds));
        Assert.Equal(120m, draft.Lines.Sum(l => l.AmountCad));
        Assert.Equal(2, draft.ClaimedTripIds.Count);
        Assert.Contains(a.Id, draft.ClaimedTripIds);
        Assert.Contains(b.Id, draft.ClaimedTripIds);
    }

    [Fact]
    public void Legs_missing_direction_do_not_pair_even_when_two_share_a_key()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // Same key, both directions null (pre-migration rows) — no valid pair.
        var a = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-null", "TR-A",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero));
        var b = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-null", "TR-B",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero));

        var result = InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [a, b]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.All(result.Value.Lines, l => Assert.Equal(0.5m, l.Quantity));
    }

    [Fact]
    public void Multiple_round_trips_produce_one_line_each_ordered_by_service_date()
    {
        var contract = TestBilling.Contract(rate: 100m);
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-2");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-1");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [o1, r1, o2, r2]);

        Assert.True(result.IsSuccess);
        var draft = result.Value;
        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal(new DateOnly(2026, 7, 6), draft.Lines[0].ServiceDate);
        Assert.Equal(new DateOnly(2026, 7, 13), draft.Lines[1].ServiceDate);
        Assert.Equal(4, draft.ClaimedTripIds.Count);
        Assert.Equal(200m, draft.Lines.Sum(l => l.AmountCad));
    }

    [Fact]
    public void Trips_without_round_trip_key_are_not_priced_and_not_claimed()
    {
        var contract = TestBilling.Contract();
        var adHoc = TestBilling.Trip(new DateOnly(2026, 7, 10), roundTripKey: null, "TR-9001");
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [adHoc, outbound, returnLeg]);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Lines);
        Assert.DoesNotContain(adHoc.Id, result.Value.ClaimedTripIds);
    }

    [Fact]
    public void Trips_outside_the_period_are_filtered_out()
    {
        var contract = TestBilling.Contract();
        var (inO, inR) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-in");
        var (outO, outR) = TestBilling.RoundTrip(new DateOnly(2026, 8, 3), "rt-out");
        var (beforeO, beforeR) = TestBilling.RoundTrip(new DateOnly(2026, 6, 29), "rt-before");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [inO, inR, outO, outR, beforeO, beforeR]);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(new DateOnly(2026, 7, 6), line.ServiceDate);
        Assert.Equal(2, result.Value.ClaimedTripIds.Count);
    }

    [Fact]
    public void Already_invoiced_trips_are_skipped()
    {
        var contract = TestBilling.Contract();
        var claimed = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", invoiceId: Guid.NewGuid());
        var free = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1");

        var result = InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [claimed, free]);

        Assert.True(result.IsSuccess);
        // Only the free leg survives — it prices as an orphan half line.
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(0.5m, line.Quantity);
        Assert.Equal([free.Id], result.Value.ClaimedTripIds);
    }

    [Fact]
    public void Manual_billing_contract_is_rejected()
    {
        var contract = TestBilling.Contract(billingModel: "Manual", rate: null);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg]);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.NotRoundTripBilled", result.Error.Code);
    }

    [Fact]
    public void No_contract_covering_the_period_is_rejected()
    {
        // Contract ended before the period starts.
        var ended = TestBilling.Contract(
            startDate: new DateOnly(2025, 1, 1), endDate: new DateOnly(2026, 6, 30));

        var result = InvoiceDraftBuilder.Build(
            [ended], PeriodStart, PeriodEnd, []);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.NoActiveContract", result.Error.Code);
    }

    [Fact]
    public void No_contracts_at_all_is_rejected()
    {
        var result = InvoiceDraftBuilder.Build([], PeriodStart, PeriodEnd, []);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.NoActiveContract", result.Error.Code);
    }

    [Fact]
    public void Renewal_boundary_picks_the_contract_with_the_latest_start()
    {
        var old = TestBilling.Contract(
            rate: 100m, startDate: new DateOnly(2025, 1, 1), endDate: new DateOnly(2026, 7, 15));
        var renewal = TestBilling.Contract(rate: 140m, startDate: new DateOnly(2026, 7, 16));
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 20), "rt-1");

        var result = InvoiceDraftBuilder.Build(
            [old, renewal], PeriodStart, PeriodEnd, [outbound, returnLeg]);

        Assert.True(result.IsSuccess);
        Assert.Equal(renewal.Id, result.Value.Contract.Id);
        Assert.Equal(140m, Assert.Single(result.Value.Lines).UnitPriceCad);
    }

    [Fact]
    public void Gst_is_five_percent_of_subtotal_when_applicable()
    {
        var contract = TestBilling.Contract(rate: 120m, gstApplicable: true);
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-2");

        var draft = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [o1, r1, o2, r2]).Value;

        var invoice = Invoice.CreateDraft(
            TestBilling.TenantId, "INV-0001", TestBilling.ClientId, contract.ClientName,
            contract.Id, contract.DefaultPoNumber, contract.BudgetCode, contract.NetTermsDays,
            contract.GstApplicable, Invoice.StandardGstRate,
            PeriodStart, PeriodEnd, draft.Lines).Value;

        Assert.Equal(240m, invoice.SubtotalCad);
        Assert.Equal(12m, invoice.GstCad);
        Assert.Equal(252m, invoice.TotalCad);
    }

    [Fact]
    public void Gst_is_absent_when_contract_is_not_gst_applicable()
    {
        var contract = TestBilling.Contract(rate: 120m, gstApplicable: false);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg]).Value;

        var invoice = Invoice.CreateDraft(
            TestBilling.TenantId, "INV-0001", TestBilling.ClientId, contract.ClientName,
            contract.Id, contract.DefaultPoNumber, contract.BudgetCode, contract.NetTermsDays,
            contract.GstApplicable, Invoice.StandardGstRate,
            PeriodStart, PeriodEnd, draft.Lines).Value;

        Assert.Equal(120m, invoice.SubtotalCad);
        Assert.Equal(0m, invoice.GstCad);
        Assert.Equal(120m, invoice.TotalCad);
    }

    [Fact]
    public void Inverted_period_is_rejected()
    {
        var result = InvoiceDraftBuilder.Build(
            [TestBilling.Contract()], PeriodEnd, PeriodStart, []);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.InvalidPeriod", result.Error.Code);
    }
}
