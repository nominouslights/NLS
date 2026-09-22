using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// The pricing rule: one worksheet per effective PO, each PO's own terms overriding the
/// contract rate, with the fallbacks (contract rate; half the round-trip rate for one-way)
/// producing exactly today's amounts for a client that has no PO terms at all.
/// </summary>
public class InvoiceDraftBuilderTests
{
    private static readonly DateOnly PeriodStart = new(2026, 7, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 31);

    /// <summary>Generation always produces at least one worksheet; most cases produce exactly one.</summary>
    private static InvoiceDraft OnlyDraft(Result<IReadOnlyList<InvoiceDraft>> result)
    {
        Assert.True(result.IsSuccess);
        return Assert.Single(result.Value);
    }

    [Fact]
    public void Complete_round_trip_pair_prices_as_one_line_at_full_rate()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg]));

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
        Assert.Equal(TestBilling.DefaultPo, draft.PoNumber);
    }

    [Fact]
    public void Orphan_leg_prices_one_way_at_half_the_round_trip_rate_when_the_po_sets_none()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 8), "rt-lonely", "TR-4830");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [orphan]));

        var line = Assert.Single(draft.Lines);
        // Quantity 1 at the derived one-way rate — the same $60 the old 0.5 × $120 produced.
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(60m, line.UnitPriceCad);
        Assert.Equal(60m, line.AmountCad);
        Assert.Contains(InvoiceDraftBuilder.OneWayLegFlag, line.Description);
        Assert.Equal("TR-4830", line.TripNumber);
        Assert.Equal([orphan.Id], draft.ClaimedTripIds);
    }

    [Fact]
    public void Group_pairing_an_outbound_with_an_inbound_prices_as_one_full_line()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var outbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", "TR-01O", direction: "Outbound");
        var inbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", "TR-01R", direction: "Inbound");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, inbound]));

        var line = Assert.Single(draft.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(120m, line.AmountCad);
        Assert.StartsWith("Corridor round trip", line.Description);
        Assert.Equal(2, line.TripIds.Count);
        Assert.Equal(2, draft.ClaimedTripIds.Count);
    }

    [Fact]
    public void Deadhead_pair_prices_at_full_rate_with_the_discount_optional_note()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var outbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "merge:abc", "TR-01O",
            direction: "Outbound",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero));
        var deadhead = TestBilling.Trip(new DateOnly(2026, 7, 6), "merge:abc", "TR-01R",
            direction: "Inbound", isEmptyLeg: true,
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero));

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, deadhead]));

        var line = Assert.Single(draft.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(120m, line.UnitPriceCad);
        Assert.Equal(120m, line.AmountCad); // full rate — the discount is a manual worksheet edit
        Assert.Contains(InvoiceDraftBuilder.DeadheadReturnFlag, line.Description);
        Assert.Contains("Thompson–Lynn Lake", line.Description);
        Assert.Equal(2, line.TripIds.Count);
        Assert.Equal(2, draft.ClaimedTripIds.Count);
    }

    [Fact]
    public void Regular_pair_carries_no_deadhead_note()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg]));

        Assert.DoesNotContain(
            InvoiceDraftBuilder.DeadheadReturnFlag,
            Assert.Single(draft.Lines).Description);
    }

    [Fact]
    public void Two_legs_sharing_a_direction_are_not_a_pair_and_bill_as_two_one_way_lines()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // Two Outbound legs on the same key — a data anomaly, never a valid round trip.
        var a = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-dup", "TR-A", direction: "Outbound",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero));
        var b = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-dup", "TR-B", direction: "Outbound",
            completedAtUtc: new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero));

        var draft = OnlyDraft(InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [a, b]));

        Assert.Equal(2, draft.Lines.Count);
        Assert.All(draft.Lines, l => Assert.Equal(1m, l.Quantity));
        Assert.All(draft.Lines, l => Assert.Equal(60m, l.UnitPriceCad));
        Assert.All(draft.Lines, l => Assert.Contains(InvoiceDraftBuilder.OneWayLegFlag, l.Description));
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

        var draft = OnlyDraft(InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [a, b]));

        Assert.Equal(2, draft.Lines.Count);
        Assert.All(draft.Lines, l => Assert.Equal(1m, l.Quantity));
        Assert.All(draft.Lines, l => Assert.Equal(60m, l.UnitPriceCad));
    }

    [Fact]
    public void Multiple_round_trips_produce_one_line_each_ordered_by_service_date()
    {
        var contract = TestBilling.Contract(rate: 100m);
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-2");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [o1, r1, o2, r2]));

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

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [adHoc, outbound, returnLeg]));

        Assert.Single(draft.Lines);
        Assert.DoesNotContain(adHoc.Id, draft.ClaimedTripIds);
    }

    [Fact]
    public void Trips_outside_the_period_are_filtered_out()
    {
        var contract = TestBilling.Contract();
        var (inO, inR) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-in");
        var (outO, outR) = TestBilling.RoundTrip(new DateOnly(2026, 8, 3), "rt-out");
        var (beforeO, beforeR) = TestBilling.RoundTrip(new DateOnly(2026, 6, 29), "rt-before");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [inO, inR, outO, outR, beforeO, beforeR]));

        var line = Assert.Single(draft.Lines);
        Assert.Equal(new DateOnly(2026, 7, 6), line.ServiceDate);
        Assert.Equal(2, draft.ClaimedTripIds.Count);
    }

    [Fact]
    public void Already_invoiced_trips_are_skipped()
    {
        var contract = TestBilling.Contract();
        var claimed = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", invoiceId: Guid.NewGuid());
        var free = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, [claimed, free]));

        // Only the free leg survives — it prices as a lone one-way leg.
        var line = Assert.Single(draft.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(60m, line.UnitPriceCad);
        Assert.Equal([free.Id], draft.ClaimedTripIds);
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

    /// <summary>
    /// Manual stays manual by decision: PO rates may be recorded for such a client, but they do
    /// not make automatic drafting possible. Changing that is a change to the one guard in the
    /// builder, not to the pricing rule.
    /// </summary>
    [Fact]
    public void Manual_billing_contract_is_still_rejected_even_when_the_po_carries_explicit_rates()
    {
        var contract = TestBilling.Contract(billingModel: "Manual", rate: null);
        var purchaseOrder = TestBilling.PurchaseOrder(roundTripRateCad: 300m, oneWayRateCad: 180m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder]);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.NotRoundTripBilled", result.Error.Code);
    }

    [Fact]
    public void No_contract_covering_the_period_is_rejected()
    {
        // Contract ended before the period starts.
        var ended = TestBilling.Contract(
            startDate: new DateOnly(2025, 1, 1), endDate: new DateOnly(2026, 6, 30));

        var result = InvoiceDraftBuilder.Build([ended], PeriodStart, PeriodEnd, []);

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

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [old, renewal], PeriodStart, PeriodEnd, [outbound, returnLeg]));

        Assert.Equal(renewal.Id, draft.Contract.Id);
        Assert.Equal(140m, Assert.Single(draft.Lines).UnitPriceCad);
    }

    [Fact]
    public void Empty_period_still_yields_one_zero_line_worksheet_on_the_contract_default_po()
    {
        var contract = TestBilling.Contract();

        var draft = OnlyDraft(InvoiceDraftBuilder.Build([contract], PeriodStart, PeriodEnd, []));

        Assert.Empty(draft.Lines);
        Assert.Empty(draft.ClaimedTripIds);
        Assert.Equal(TestBilling.DefaultPo, draft.PoNumber);
    }

    /// <summary>
    /// A drafted invoice's total is exactly the sum of the lines the builder priced — no tax is
    /// added at any point. The platform never computes GST/HST/PST; QuickBooks Online does.
    /// </summary>
    [Fact]
    public void Drafted_invoice_total_is_the_sum_of_its_lines_with_no_tax_added()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-2");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [o1, r1, o2, r2]));

        var invoice = Invoice.CreateDraft(
            TestBilling.TenantId, "INV-0001", TestBilling.ClientId, contract.ClientName,
            contract.Id, draft.PoNumber, contract.BudgetCode, contract.NetTermsDays,
            PeriodStart, PeriodEnd, draft.Lines).Value;

        // Two round trips at $120 — $240, not $252.
        Assert.Equal(240m, invoice.TotalCad);
        Assert.Equal(draft.Lines.Sum(line => line.AmountCad), invoice.TotalCad);
    }

    [Fact]
    public void Inverted_period_is_rejected()
    {
        var result = InvoiceDraftBuilder.Build(
            [TestBilling.Contract()], PeriodEnd, PeriodStart, []);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.InvalidPeriod", result.Error.Code);
    }

    // ---- Per-PO pricing ----

    [Fact]
    public void Po_round_trip_rate_overrides_the_contract_rate()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var purchaseOrder = TestBilling.PurchaseOrder(roundTripRateCad: 305m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder]));

        var line = Assert.Single(draft.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(305m, line.UnitPriceCad);
        Assert.Equal(305m, line.AmountCad);
        Assert.Equal(TestBilling.DefaultPo, draft.PoNumber);
        Assert.Same(purchaseOrder, draft.PurchaseOrder);
    }

    [Fact]
    public void Po_without_a_round_trip_rate_falls_back_to_the_contract_rate()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // A PO that records only its value and window — no terms of its own.
        var purchaseOrder = TestBilling.PurchaseOrder(amountCad: 50_000m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder]));

        Assert.Equal(120m, Assert.Single(draft.Lines).UnitPriceCad);
    }

    [Fact]
    public void Explicit_po_one_way_rate_prices_a_lone_leg_instead_of_half_the_round_trip()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // Deliberately NOT half of the round-trip rate — a separately negotiated figure.
        var purchaseOrder = TestBilling.PurchaseOrder(roundTripRateCad: 300m, oneWayRateCad: 200m);
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 8), "rt-lonely", "TR-4830");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [orphan], [purchaseOrder]));

        var line = Assert.Single(draft.Lines);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(200m, line.UnitPriceCad);
        Assert.Equal(200m, line.AmountCad);
        Assert.Contains(InvoiceDraftBuilder.OneWayLegFlag, line.Description);
    }

    [Fact]
    public void One_way_falls_back_to_half_the_effective_round_trip_rate_when_the_po_sets_none()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // PO sets a round-trip rate only — the half rule applies to the PO's rate, not the contract's.
        var purchaseOrder = TestBilling.PurchaseOrder(roundTripRateCad: 300m);
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 8), "rt-lonely", "TR-4830");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [orphan], [purchaseOrder]));

        var line = Assert.Single(draft.Lines);
        Assert.Equal(150m, line.UnitPriceCad);
        Assert.Equal(150m, line.AmountCad);
    }

    [Fact]
    public void A_pair_split_across_two_pos_prices_as_two_one_way_lines_on_two_worksheets()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", roundTripRateCad: 300m, oneWayRateCad: 170m);
        var poB = TestBilling.PurchaseOrder("PO-B", roundTripRateCad: 400m, oneWayRateCad: 210m);

        var outbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-split", "TR-01O",
            direction: "Outbound", poNumber: "PO-A");
        var inbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-split", "TR-01R",
            direction: "Inbound", poNumber: "PO-B");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, inbound], [poA, poB]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var draftA = result.Value[0];
        var draftB = result.Value[1];
        Assert.Equal("PO-A", draftA.PoNumber);
        Assert.Equal("PO-B", draftB.PoNumber);

        var lineA = Assert.Single(draftA.Lines);
        var lineB = Assert.Single(draftB.Lines);
        Assert.Equal(170m, lineA.UnitPriceCad);
        Assert.Equal(210m, lineB.UnitPriceCad);
        Assert.All<InvoiceLine>([lineA, lineB], l => Assert.Equal(1m, l.Quantity));
        Assert.All<InvoiceLine>(
            [lineA, lineB], l => Assert.Contains(InvoiceDraftBuilder.SplitPurchaseOrderFlag, l.Description));
        Assert.All<InvoiceLine>(
            [lineA, lineB], l => Assert.Contains(InvoiceDraftBuilder.OneWayLegFlag, l.Description));

        // No trip claimed twice across the worksheets.
        var allClaimed = result.Value.SelectMany(d => d.ClaimedTripIds).ToList();
        Assert.Equal(allClaimed.Count, allClaimed.Distinct().Count());
        Assert.Equal([outbound.Id], draftA.ClaimedTripIds);
        Assert.Equal([inbound.Id], draftB.ClaimedTripIds);
    }

    [Fact]
    public void A_period_spanning_two_pos_produces_one_worksheet_per_po()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", roundTripRateCad: 300m);
        var poB = TestBilling.PurchaseOrder("PO-B", roundTripRateCad: 400m);

        var (a1, a2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-A", poNumber: "PO-A");
        var (b1, b2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-B", poNumber: "PO-B");

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [a1, a2, b1, b2], [poA, poB]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(["PO-A", "PO-B"], result.Value.Select(d => d.PoNumber));
        Assert.Equal(300m, Assert.Single(result.Value[0].Lines).AmountCad);
        Assert.Equal(400m, Assert.Single(result.Value[1].Lines).AmountCad);
        Assert.Equal([a1.Id, a2.Id], result.Value[0].ClaimedTripIds.ToHashSet());
        Assert.Equal([b1.Id, b2.Id], result.Value[1].ClaimedTripIds.ToHashSet());

        var allClaimed = result.Value.SelectMany(d => d.ClaimedTripIds).ToList();
        Assert.Equal(4, allClaimed.Count);
        Assert.Equal(4, allClaimed.Distinct().Count());
    }

    [Fact]
    public void Work_with_no_po_at_all_still_gets_its_own_worksheet()
    {
        // No trip PO and no contract default: the work is not dropped on the floor.
        var contract = TestBilling.Contract(rate: 120m, defaultPoNumber: null);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1", poNumber: null);

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg]));

        Assert.Null(draft.PoNumber);
        Assert.Null(draft.PurchaseOrder);
        Assert.Equal(120m, Assert.Single(draft.Lines).AmountCad);
        Assert.Equal(2, draft.ClaimedTripIds.Count);
    }

    [Fact]
    public void The_no_po_worksheet_sorts_last_behind_the_po_worksheets()
    {
        var contract = TestBilling.Contract(rate: 120m, defaultPoNumber: null);
        var (withPo1, withPo2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 20), "rt-po", "TR-P", poNumber: "PO-Z");
        var (noPo1, noPo2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-none", "TR-N", poNumber: null);

        var result = InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [withPo1, withPo2, noPo1, noPo2]);

        Assert.True(result.IsSuccess);
        Assert.Equal(["PO-Z", null], result.Value.Select(d => d.PoNumber));
    }

    [Fact]
    public void A_leg_outside_the_po_window_is_flagged_but_still_priced()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var purchaseOrder = TestBilling.PurchaseOrder(
            issued: new DateOnly(2026, 7, 1), expiry: new DateOnly(2026, 7, 10), roundTripRateCad: 300m);
        // Service on the 20th — past the PO's expiry.
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 20), "rt-late");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder]));

        var line = Assert.Single(draft.Lines);
        Assert.Contains(InvoiceDraftBuilder.OutsidePurchaseOrderWindowFlag, line.Description);
        Assert.Equal(300m, line.AmountCad); // flagged, not refused and not repriced
    }

    [Fact]
    public void A_leg_inside_the_po_window_is_not_flagged()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var purchaseOrder = TestBilling.PurchaseOrder(
            issued: new DateOnly(2026, 7, 1), expiry: new DateOnly(2026, 7, 31), roundTripRateCad: 300m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 20), "rt-ok");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder]));

        Assert.DoesNotContain(
            InvoiceDraftBuilder.OutsidePurchaseOrderWindowFlag,
            Assert.Single(draft.Lines).Description);
        Assert.Empty(draft.Warnings);
    }

    [Fact]
    public void Exceeding_the_po_value_warns_but_the_draft_is_still_produced()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var purchaseOrder = TestBilling.PurchaseOrder(amountCad: 500m, roundTripRateCad: 300m);
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1", "TR-1");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-2", "TR-2");

        // $600 of new work plus $100 already invoiced against a $500 PO.
        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [o1, r1, o2, r2], [purchaseOrder],
            new Dictionary<string, decimal> { [TestBilling.DefaultPo] = 100m }));

        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal(600m, draft.TotalCad);
        var warning = Assert.Single(draft.Warnings);
        Assert.StartsWith(InvoiceDraftBuilder.PurchaseOrderValueExceededFlag, warning);
        Assert.Contains(TestBilling.DefaultPo, warning);
    }

    [Fact]
    public void Staying_within_the_po_value_produces_no_warning()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var purchaseOrder = TestBilling.PurchaseOrder(amountCad: 1_000m, roundTripRateCad: 300m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder],
            new Dictionary<string, decimal> { [TestBilling.DefaultPo] = 100m }));

        Assert.Empty(draft.Warnings);
    }

    [Fact]
    public void A_po_with_no_recorded_value_can_never_be_exceeded()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var purchaseOrder = TestBilling.PurchaseOrder(amountCad: null, roundTripRateCad: 300m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [purchaseOrder],
            new Dictionary<string, decimal> { [TestBilling.DefaultPo] = 999_999m }));

        Assert.Empty(draft.Warnings);
    }

    /// <summary>
    /// The no-regression case: a client with no PO terms at all invoices for exactly what it
    /// invoiced before per-PO pricing existed — one worksheet, the same amounts, and every leg
    /// priced off the contract rate. Only the quantity/unit-price split changed on a one-way
    /// line (1 × 0.5r instead of 0.5 × r), which is the same money.
    /// </summary>
    [Fact]
    public void A_client_with_no_po_terms_invoices_exactly_the_same_amounts_as_before()
    {
        var contract = TestBilling.Contract(rate: 120m);
        // A PO row exists but carries no terms — as every migrated PO does.
        var termlessPo = TestBilling.PurchaseOrder();
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1", "TR-1");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-2", "TR-2");
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 20), "rt-lonely", "TR-3");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [o1, r1, o2, r2, orphan], [termlessPo]));

        // $120 + $120 + $60 — identical to the pre-PO behaviour.
        Assert.Equal(3, draft.Lines.Count);
        Assert.Equal(300m, draft.TotalCad);
        Assert.Equal(5, draft.ClaimedTripIds.Count);
        Assert.Empty(draft.Warnings);
        Assert.Equal(TestBilling.DefaultPo, draft.PoNumber);
    }

    [Fact]
    public void Line_amount_stays_quantity_times_unit_price_rounded_to_cents()
    {
        var contract = TestBilling.Contract(rate: 333.33m);
        var purchaseOrder = TestBilling.PurchaseOrder(roundTripRateCad: 333.33m);
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 8), "rt-lonely", "TR-4830");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [orphan], [purchaseOrder]));

        var line = Assert.Single(draft.Lines);
        // Half of $333.33 is $166.665; the amount is that rounded to cents, banker's rounding
        // and all — the same Math.Round(Quantity × UnitPriceCad, 2) InvoiceLine has always used.
        Assert.Equal(166.665m, line.UnitPriceCad);
        Assert.Equal(Math.Round(line.Quantity * line.UnitPriceCad, 2), line.AmountCad);
        Assert.Equal(166.66m, line.AmountCad);
    }

    [Fact]
    public void Po_numbers_match_case_insensitively_so_one_worksheet_covers_both_spellings()
    {
        var contract = TestBilling.Contract(rate: 120m, defaultPoNumber: null);
        var purchaseOrder = TestBilling.PurchaseOrder("po-a", roundTripRateCad: 300m);
        var outbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", "TR-O",
            direction: "Outbound", poNumber: "PO-A");
        var inbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", "TR-R",
            direction: "Inbound", poNumber: "po-a");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, inbound], [purchaseOrder]));

        // One bucket, so still a real round trip — not two split one-way legs.
        var line = Assert.Single(draft.Lines);
        Assert.Equal(300m, line.AmountCad);
        Assert.DoesNotContain(InvoiceDraftBuilder.SplitPurchaseOrderFlag, line.Description);
    }

    [Fact]
    public void A_trip_po_overrides_the_contract_default_po()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var contractPo = TestBilling.PurchaseOrder(TestBilling.DefaultPo, roundTripRateCad: 300m);
        var tripPo = TestBilling.PurchaseOrder("PO-OVERRIDE", roundTripRateCad: 999m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(
            new DateOnly(2026, 7, 6), "rt-1", poNumber: "PO-OVERRIDE");

        var draft = OnlyDraft(InvoiceDraftBuilder.Build(
            [contract], PeriodStart, PeriodEnd, [outbound, returnLeg], [contractPo, tripPo]));

        Assert.Equal("PO-OVERRIDE", draft.PoNumber);
        Assert.Equal(999m, Assert.Single(draft.Lines).UnitPriceCad);
    }
}
