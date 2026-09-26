using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Domain.PurchaseOrders;

namespace NorthernLink.Billing.Tests;

/// <summary>Canonical test fixtures for the Billing unit tests.</summary>
public static class TestBilling
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid ClientId = Guid.NewGuid();

    /// <summary>The PO number both the default contract and the default trip carry.</summary>
    public const string DefaultPo = "PO-7781";

    public static ContractSnapshot Contract(
        string billingModel = ContractSnapshot.RoundTripRateBillingModel,
        decimal? rate = 120m,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string status = "Active",
        string? defaultPoNumber = DefaultPo) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ClientId = ClientId,
        ClientName = "Lynn Lake Mining Co.",
        StartDate = startDate ?? new DateOnly(2026, 1, 1),
        EndDate = endDate,
        BillingModel = billingModel,
        RatePerRoundTripCad = rate,
        BudgetCode = "ZBB-CREW-01",
        BillingFrequency = "Monthly",
        NetTermsDays = 30,
        DefaultPoNumber = defaultPoNumber,
        Status = status,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// A PO replica row. <paramref name="roundTripRateCad"/> defaults to null — "no PO term",
    /// so the contract rate applies. <paramref name="oneWayRateCad"/> is still stored and
    /// replicated but no longer prices anything; it is here so tests can prove exactly that.
    /// </summary>
    public static PurchaseOrderSnapshot PurchaseOrder(
        string poNumber = DefaultPo,
        DateOnly? issued = null,
        DateOnly? expiry = null,
        decimal? amountCad = null,
        decimal? roundTripRateCad = null,
        decimal? oneWayRateCad = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ClientId = ClientId,
        PoNumber = poNumber,
        Issued = issued ?? new DateOnly(2026, 1, 1),
        Expiry = expiry,
        AmountCad = amountCad,
        RoundTripRateCad = roundTripRateCad,
        OneWayRateCad = oneWayRateCad,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    public static BillableTrip Trip(
        DateOnly serviceDate,
        string? roundTripKey,
        string tripNumber = "TR-4821",
        Guid? invoiceId = null,
        DateTimeOffset? completedAtUtc = null,
        string? direction = null,
        bool isEmptyLeg = false,
        string? poNumber = DefaultPo) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        TripNumber = tripNumber,
        ClientId = ClientId,
        ClientName = "Lynn Lake Mining Co.",
        ServiceType = "ContractCrew",
        RouteName = "Thompson–Lynn Lake",
        Origin = "Thompson",
        Destination = "Lynn Lake",
        DistanceKm = 320,
        ServiceDate = serviceDate,
        RoundTripKey = roundTripKey,
        Direction = direction,
        IsEmptyLeg = isEmptyLeg,
        PoNumber = poNumber,
        CompletedAtUtc = completedAtUtc ?? serviceDate.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc),
        InvoiceId = invoiceId,
    };

    /// <summary>An Outbound + Inbound pair sharing one round-trip key — a complete round trip.</summary>
    public static (BillableTrip Outbound, BillableTrip Return) RoundTrip(
        DateOnly serviceDate,
        string key,
        string tripNumberPrefix = "TR-48",
        string? poNumber = DefaultPo)
    {
        var outbound = Trip(serviceDate, key, $"{tripNumberPrefix}O",
            completedAtUtc: serviceDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            direction: "Outbound",
            poNumber: poNumber);
        var returnLeg = Trip(serviceDate, key, $"{tripNumberPrefix}R",
            completedAtUtc: serviceDate.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc),
            direction: "Inbound",
            poNumber: poNumber);
        return (outbound, returnLeg);
    }

    public static InvoiceLine Line(decimal quantity = 1m, decimal unitPrice = 120m, params Guid[] tripIds) =>
        InvoiceLine.Create("Corridor round trip · Thompson–Lynn Lake · 2026-07-06",
            tripIds, null, new DateOnly(2026, 7, 6), quantity, unitPrice).Value;

    public static Invoice DraftInvoice(params InvoiceLine[] lines) =>
        Invoice.CreateDraft(
            TenantId,
            "INV-0001",
            ClientId,
            "Lynn Lake Mining Co.",
            Guid.NewGuid(),
            "PO-7781",
            "ZBB-CREW-01",
            30,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            lines).Value;
}
