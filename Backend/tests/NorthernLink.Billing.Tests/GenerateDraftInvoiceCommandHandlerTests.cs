using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Application.Invoices;
using NorthernLink.Billing.Application.Invoices.GenerateDraft;
using NorthernLink.Billing.Domain.Invoices;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// One generation request now produces one worksheet per effective PO. These pin the parts the
/// builder cannot: each worksheet gets its own invoice number, stamps its own PO, no trip is
/// claimed twice across the set, and the whole operation commits in one save.
/// </summary>
public class GenerateDraftInvoiceCommandHandlerTests
{
    private static readonly DateOnly PeriodStart = new(2026, 7, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 31);

    private sealed class InMemoryInvoiceRepository : IInvoiceRepository
    {
        public List<Invoice> Invoices { get; } = [];

        public int SaveCount { get; private set; }

        public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));

        public void Add(Invoice invoice) => Invoices.Add(invoice);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Mirrors the real generator's contract: the whole batch derives from one count of
    /// already-persisted invoices, so numbers inside a generation cannot repeat.
    /// </summary>
    private sealed class FakeInvoiceNumberGenerator(int alreadyIssued = 0) : IInvoiceNumberGenerator
    {
        public Task<IReadOnlyList<string>> NextInvoiceNumbersAsync(
            Guid tenantId, int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                [.. Enumerable.Range(alreadyIssued + 1, count).Select(n => $"INV-{n:D4}")]);
    }

    private sealed class FakeInvoiceReadService(Dictionary<string, decimal>? invoicedByPo = null) : IInvoiceReadService
    {
        public Task<IReadOnlyList<InvoiceSummaryResponse>> GetInvoicesAsync(
            InvoiceStatus? status, Guid? clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InvoiceSummaryResponse>>([]);

        public Task<IReadOnlyDictionary<string, decimal>> GetInvoicedTotalsByPoNumberAsync(
            Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, decimal>>(
                invoicedByPo ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));
    }

    private static (GenerateDraftInvoiceCommandHandler Handler,
        InMemoryInvoiceRepository Invoices,
        InMemoryBillableTripRepository Trips) Build(
        IEnumerable<Domain.Contracts.ContractSnapshot> contracts,
        IEnumerable<Domain.PurchaseOrders.PurchaseOrderSnapshot> purchaseOrders,
        IEnumerable<Domain.BillableTrips.BillableTrip> trips,
        Dictionary<string, decimal>? invoicedByPo = null,
        int alreadyIssued = 0)
    {
        var contractRepo = new InMemoryContractSnapshotRepository();
        foreach (var contract in contracts)
        {
            contractRepo.Add(contract);
        }

        var poRepo = new InMemoryPurchaseOrderSnapshotRepository();
        foreach (var purchaseOrder in purchaseOrders)
        {
            poRepo.Add(purchaseOrder);
        }

        var tripRepo = new InMemoryBillableTripRepository();
        foreach (var trip in trips)
        {
            tripRepo.Add(trip);
        }

        var invoiceRepo = new InMemoryInvoiceRepository();
        var handler = new GenerateDraftInvoiceCommandHandler(
            contractRepo,
            poRepo,
            tripRepo,
            invoiceRepo,
            new FakeInvoiceReadService(invoicedByPo),
            new FakeInvoiceNumberGenerator(alreadyIssued));

        return (handler, invoiceRepo, tripRepo);
    }

    private static GenerateDraftInvoiceCommand Command() =>
        new(TestBilling.TenantId, TestBilling.ClientId, PeriodStart, PeriodEnd);

    [Fact]
    public async Task A_period_spanning_two_pos_creates_two_worksheets_with_their_own_numbers_and_po_stamps()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", roundTripRateCad: 300m);
        var poB = TestBilling.PurchaseOrder("PO-B", roundTripRateCad: 400m);
        var (a1, a2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-A", poNumber: "PO-A");
        var (b1, b2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-B", poNumber: "PO-B");

        var (handler, invoices, trips) = Build([contract], [poA, poB], [a1, a2, b1, b2]);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Invoices.Count);
        Assert.Equal(["PO-A", "PO-B"], result.Value.Invoices.Select(i => i.PoNumber));
        Assert.Equal(["INV-0001", "INV-0002"], result.Value.Invoices.Select(i => i.InvoiceNumber));
        Assert.Equal([300m, 400m], result.Value.Invoices.Select(i => i.TotalCad));

        Assert.Equal(2, invoices.Invoices.Count);
        Assert.Equal(["PO-A", "PO-B"], invoices.Invoices.Select(i => i.PoNumber).Order());
        // Each worksheet number is distinct — one count, one batch.
        Assert.Equal(2, invoices.Invoices.Select(i => i.InvoiceNumber).Distinct().Count());

        // Every trip claimed exactly once, and by the invoice covering its own PO.
        Assert.All(trips.Trips, t => Assert.NotNull(t.InvoiceId));
        Assert.Equal(2, trips.Trips.Select(t => t.InvoiceId).Distinct().Count());
        var poAInvoice = invoices.Invoices.Single(i => i.PoNumber == "PO-A");
        Assert.Equal(
            [a1.Id, a2.Id],
            trips.Trips.Where(t => t.InvoiceId == poAInvoice.Id).Select(t => t.Id).ToHashSet());
    }

    [Fact]
    public async Task No_trip_is_claimed_by_two_worksheets_even_when_a_pair_is_split_across_pos()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", roundTripRateCad: 300m);
        var poB = TestBilling.PurchaseOrder("PO-B", roundTripRateCad: 400m);
        var outbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-split", "TR-O",
            direction: "Outbound", poNumber: "PO-A");
        var inbound = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-split", "TR-R",
            direction: "Inbound", poNumber: "PO-B");

        var (handler, invoices, trips) = Build([contract], [poA, poB], [outbound, inbound]);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Invoices.Count);
        var claimedIds = trips.Trips.Select(t => t.InvoiceId).ToList();
        Assert.All(claimedIds, id => Assert.NotNull(id));
        Assert.Equal(2, claimedIds.Distinct().Count());
        Assert.Equal(2, invoices.Invoices.Sum(i => i.Lines.Count));
    }

    [Fact]
    public async Task A_period_with_no_po_at_all_creates_one_null_po_worksheet()
    {
        var contract = TestBilling.Contract(rate: 120m, defaultPoNumber: null);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1", poNumber: null);

        var (handler, invoices, _) = Build([contract], [], [outbound, returnLeg]);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var generated = Assert.Single(result.Value.Invoices);
        Assert.Null(generated.PoNumber);
        Assert.Equal(120m, generated.TotalCad);
        Assert.Null(Assert.Single(invoices.Invoices).PoNumber);
    }

    /// <summary>
    /// The no-regression case: a single-PO client still produces exactly one worksheet, with the
    /// same total and the same PO stamp it got before per-PO pricing existed.
    /// </summary>
    [Fact]
    public async Task A_single_po_client_produces_one_worksheet_identical_to_the_previous_behaviour()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var (o1, r1) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1", "TR-1");
        var (o2, r2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-2", "TR-2");
        var orphan = TestBilling.Trip(new DateOnly(2026, 7, 20), "rt-lonely", "TR-3");

        var (handler, invoices, trips) = Build([contract], [], [o1, r1, o2, r2, orphan]);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var generated = Assert.Single(result.Value.Invoices);
        Assert.Equal(TestBilling.DefaultPo, generated.PoNumber);
        Assert.Equal("INV-0001", generated.InvoiceNumber);
        Assert.Equal(3, generated.LineCount);
        Assert.Equal(300m, generated.TotalCad); // 120 + 120 + 60, exactly as before
        Assert.Empty(generated.Warnings);

        var invoice = Assert.Single(invoices.Invoices);
        Assert.Equal(300m, invoice.TotalCad);
        Assert.Equal("ZBB-CREW-01", invoice.BudgetCode);
        Assert.Equal(30, invoice.NetTermsDays);
        Assert.Equal(5, trips.Trips.Count(t => t.InvoiceId == invoice.Id));
    }

    [Fact]
    public async Task The_whole_operation_commits_in_one_save()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", roundTripRateCad: 300m);
        var poB = TestBilling.PurchaseOrder("PO-B", roundTripRateCad: 400m);
        var (a1, a2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-A", poNumber: "PO-A");
        var (b1, b2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-B", poNumber: "PO-B");

        var (handler, invoices, _) = Build([contract], [poA, poB], [a1, a2, b1, b2]);

        await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(1, invoices.SaveCount);
    }

    [Fact]
    public async Task A_client_with_no_completed_trips_still_gets_one_empty_worksheet()
    {
        var contract = TestBilling.Contract(rate: 120m);

        var (handler, invoices, _) = Build([contract], [], []);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var generated = Assert.Single(result.Value.Invoices);
        Assert.Equal(0, generated.LineCount);
        Assert.Equal(0m, generated.TotalCad);
        Assert.Equal(TestBilling.DefaultPo, generated.PoNumber);
        Assert.Single(invoices.Invoices);
    }

    [Fact]
    public async Task Numbering_continues_the_tenants_sequence_across_the_batch()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", roundTripRateCad: 300m);
        var poB = TestBilling.PurchaseOrder("PO-B", roundTripRateCad: 400m);
        var (a1, a2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-A", poNumber: "PO-A");
        var (b1, b2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-B", poNumber: "PO-B");

        var (handler, _, _) = Build([contract], [poA, poB], [a1, a2, b1, b2], alreadyIssued: 41);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(["INV-0042", "INV-0043"], result.Value.Invoices.Select(i => i.InvoiceNumber));
    }

    [Fact]
    public async Task The_po_value_warning_rides_back_on_the_worksheet_it_belongs_to()
    {
        var contract = TestBilling.Contract(rate: 120m);
        var poA = TestBilling.PurchaseOrder("PO-A", amountCad: 100m, roundTripRateCad: 300m);
        var poB = TestBilling.PurchaseOrder("PO-B", amountCad: 10_000m, roundTripRateCad: 400m);
        var (a1, a2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-a", "TR-A", poNumber: "PO-A");
        var (b1, b2) = TestBilling.RoundTrip(new DateOnly(2026, 7, 13), "rt-b", "TR-B", poNumber: "PO-B");

        var (handler, invoices, _) = Build([contract], [poA, poB], [a1, a2, b1, b2]);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var overCap = result.Value.Invoices.Single(i => i.PoNumber == "PO-A");
        var withinCap = result.Value.Invoices.Single(i => i.PoNumber == "PO-B");
        Assert.Contains(InvoiceDraftBuilder.PurchaseOrderValueExceededFlag, Assert.Single(overCap.Warnings));
        Assert.Empty(withinCap.Warnings);
        // Warned, not blocked: both worksheets exist.
        Assert.Equal(2, invoices.Invoices.Count);
    }

    [Fact]
    public async Task A_manual_billing_contract_still_creates_nothing()
    {
        var contract = TestBilling.Contract(billingModel: "Manual", rate: null);
        var purchaseOrder = TestBilling.PurchaseOrder(roundTripRateCad: 300m, oneWayRateCad: 180m);
        var (outbound, returnLeg) = TestBilling.RoundTrip(new DateOnly(2026, 7, 6), "rt-1");

        var (handler, invoices, trips) = Build([contract], [purchaseOrder], [outbound, returnLeg]);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.Invoice.NotRoundTripBilled", result.Error.Code);
        Assert.Empty(invoices.Invoices);
        Assert.Equal(0, invoices.SaveCount);
        Assert.All(trips.Trips, t => Assert.Null(t.InvoiceId));
    }
}
