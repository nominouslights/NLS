using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Tests;

/// <summary>Canonical test fixtures for the Billing unit tests.</summary>
public static class TestBilling
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid ClientId = Guid.NewGuid();

    public static ContractSnapshot Contract(
        string billingModel = ContractSnapshot.RoundTripRateBillingModel,
        decimal? rate = 120m,
        bool gstApplicable = true,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string status = "Active") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ClientId = ClientId,
        ClientName = "Lynn Lake Mining Co.",
        StartDate = startDate ?? new DateOnly(2026, 1, 1),
        EndDate = endDate,
        BillingModel = billingModel,
        RatePerRoundTripCad = rate,
        GstApplicable = gstApplicable,
        BudgetCode = "ZBB-CREW-01",
        BillingFrequency = "Monthly",
        NetTermsDays = 30,
        DefaultPoNumber = "PO-7781",
        Status = status,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    public static BillableTrip Trip(
        DateOnly serviceDate,
        string? roundTripKey,
        string tripNumber = "TR-4821",
        Guid? invoiceId = null,
        DateTimeOffset? completedAtUtc = null) => new()
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
        PoNumber = "PO-7781",
        CompletedAtUtc = completedAtUtc ?? serviceDate.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc),
        InvoiceId = invoiceId,
    };

    /// <summary>An outbound + return pair sharing one round-trip key.</summary>
    public static (BillableTrip Outbound, BillableTrip Return) RoundTrip(
        DateOnly serviceDate,
        string key,
        string tripNumberPrefix = "TR-48")
    {
        var outbound = Trip(serviceDate, key, $"{tripNumberPrefix}O",
            completedAtUtc: serviceDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc));
        var returnLeg = Trip(serviceDate, key, $"{tripNumberPrefix}R",
            completedAtUtc: serviceDate.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc));
        return (outbound, returnLeg);
    }

    public static InvoiceLine Line(decimal quantity = 1m, decimal unitPrice = 120m, params Guid[] tripIds) =>
        InvoiceLine.Create("Corridor round trip · Thompson–Lynn Lake · 2026-07-06",
            tripIds, null, new DateOnly(2026, 7, 6), quantity, unitPrice).Value;

    public static Invoice DraftInvoice(
        bool gstApplicable = true,
        params InvoiceLine[] lines) =>
        Invoice.CreateDraft(
            TenantId,
            "INV-0001",
            ClientId,
            "Lynn Lake Mining Co.",
            Guid.NewGuid(),
            "PO-7781",
            "ZBB-CREW-01",
            30,
            gstApplicable,
            Invoice.StandardGstRate,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            lines).Value;
}
