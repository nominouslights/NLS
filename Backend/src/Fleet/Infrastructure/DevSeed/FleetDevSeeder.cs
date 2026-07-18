using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Domain.Documents;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.Shops;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Infrastructure.Persistence;

namespace NorthernLink.Fleet.Infrastructure.DevSeed;

/// <summary>
/// Development-only seed: the six vehicles the Dispatcher's mock data
/// (Dispatcher/lib/data.ts) shows, plus demo maintenance records (shops, documents,
/// service history, work orders, dispatcher inspections) so the merged Fleet &amp;
/// Maintenance console has real API data out of the box. U-01 is deliberately at
/// 93.6% of its service life so the depreciation / near-EOL UI is visible immediately.
/// Idempotent — skips when any vehicle already exists.
/// </summary>
public static class FleetDevSeeder
{
    public static async Task SeedAsync(FleetDbContext context, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (await context.Vehicles.AnyAsync(cancellationToken))
        {
            return;
        }

        // unit, vin, make, model, year, seats, plate, odometerKm, costCad, endOfLifeKm
        var seeds = new (string Unit, string Vin, string Make, string Model, int Year, int Seats, string Plate, int OdometerKm, decimal CostCad, int EndOfLifeKm)[]
        {
            ("U-01", "1HVBBAAN8RH553310", "International", "3000", 2016, 24, "MB · GHT 390", 468_000, 175_000m, 500_000),
            ("U-02", "1HVBBAAN2RH554471", "International", "3000", 2019, 24, "MB · GHT 402", 210_000, 182_000m, 500_000),
            ("U-03", "1HVBBAAN6RH555522", "International", "3000", 2019, 24, "MB · GHT 418", 246_000, 182_000m, 500_000),
            ("U-04", "1FTBW2CM4PKA79013", "Ford", "Transit T-150", 2022, 7, "MB · FRT 771", 88_000, 62_000m, 350_000),
            ("U-05", "1FTBW2CM8PKA79188", "Ford", "Transit T-150", 2022, 7, "MB · FRT 802", 61_500, 62_000m, 350_000),
            ("U-06", "1FTBW2CM1PKA79204", "Ford", "Transit T-150", 2021, 7, "MB · FRT 815", 132_500, 58_500m, 350_000),
        };

        var vehicles = new Dictionary<string, Vehicle>();
        foreach (var seed in seeds)
        {
            var vehicle = Vehicle.Register(
                tenantId,
                seed.Unit,
                Vin.Create(seed.Vin).Value,
                seed.Make,
                seed.Model,
                seed.Year,
                seed.Seats,
                seed.Plate,
                "Class 4",
                seed.OdometerKm,
                seed.CostCad,
                seed.EndOfLifeKm).Value;

            // Mirror the mock statuses: U-01 out of service (failed DVIR), U-06 in maintenance.
            switch (seed.Unit)
            {
                case "U-01":
                    _ = vehicle.ChangeStatus(VehicleStatus.OutOfService, "Steering fault — critical");
                    break;
                case "U-06":
                    _ = vehicle.ChangeStatus(VehicleStatus.InMaintenance, "Brake service");
                    break;
            }

            vehicles[seed.Unit] = vehicle;
            context.Vehicles.Add(vehicle);
        }

        SeedMaintenance(context, tenantId, vehicles);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void SeedMaintenance(FleetDbContext context, Guid tenantId, IReadOnlyDictionary<string, Vehicle> vehicles)
    {
        var now = DateTimeOffset.UtcNow;

        // ---- Shops / parts partners ----
        context.Shops.Add(Shop.Register(tenantId, "SHOP-01", "Thompson Certified Vehicle Inspection", "D. Fontaine", "(204) 677-4120", "service@thompsoncvi.mb.ca", "142 Hayes Rd, Thompson, MB  R8N 1M4", "81723 4471 RT0001", true, "MB-1187", false, null).Value);
        var miller = Shop.Register(tenantId, "SHOP-02", "Miller the Mover — Fleet Services", "R. Sinclair", "(204) 778-6301", "shop@millerthemover.ca", "8 Selkirk Ave, Thompson, MB  R8N 0M5", "72991 0088 RT0001", false, null, false, null).Value;
        context.Shops.Add(miller);
        context.Shops.Add(Shop.Register(tenantId, "SHOP-03", "NAPA Auto Parts — Thompson", "Parts counter", "(204) 677-2277", "thompson@napacanada.com", "83 Station Rd, Thompson, MB  R8N 0N3", null, false, null, true, null).Value);

        // ---- Compliance documents (varied expiry → valid / expiring / expired) ----
        AddDocument(context, tenantId, vehicles["U-01"].Id, "DOC-5001", DocumentType.Registration, "U-01_registration_2027.pdf", 214, now.AddYears(1));
        AddDocument(context, tenantId, vehicles["U-01"].Id, "DOC-5002", DocumentType.InsuranceMpi, "U-01_MPI_cert.pdf", 188, now.AddDays(37));
        AddDocument(context, tenantId, vehicles["U-01"].Id, "DOC-5003", DocumentType.NscSafetyCertificate, "U-01_NSC11_safety.pdf", 402, now.AddDays(-14));
        AddDocument(context, tenantId, vehicles["U-02"].Id, "DOC-5004", DocumentType.Registration, "U-02_registration.pdf", 205, now.AddYears(1).AddMonths(2));
        AddDocument(context, tenantId, vehicles["U-02"].Id, "DOC-5005", DocumentType.InsuranceMpi, "U-02_MPI_cert.pdf", 191, now.AddDays(120));
        AddDocument(context, tenantId, vehicles["U-03"].Id, "DOC-5006", DocumentType.Registration, "U-03_registration.pdf", 210, now.AddDays(52));

        // ---- Service history ----
        var svcU02 = ServiceRecord.Log(
            tenantId, vehicles["U-02"].Id, "SVC-4098", now.AddDays(-53), "R. Sinclair · Miller the Mover",
            ServiceCategory.Repair, 154_900,
            ["Front brake pads", "Front rotors (machined)"],
            "Front pads measured below 3 mm at periodic inspection; rotors scored.",
            [new ServicePart { Sku = "FLT-BRK-PAD-F", Qty = 1 }], 3m, 620m, null, null).Value;
        context.ServiceRecords.Add(svcU02);

        context.ServiceRecords.Add(ServiceRecord.Log(
            tenantId, vehicles["U-01"].Id, "SVC-4102", now.AddDays(-35), "M. Cardinal · Thompson shop",
            ServiceCategory.Preventive, 210_400,
            ["Engine oil & filter", "Air filter", "Chassis lube"],
            "Scheduled 15,000 km preventive service (mileage-based PM).",
            [new ServicePart { Sku = "FLT-OIL-15W40", Qty = 1 }, new ServicePart { Sku = "FLT-AIRFLT", Qty = 1 }],
            2.5m, 480m, null, null).Value);

        // ---- Work orders ----
        context.WorkOrders.Add(WorkOrder.Create(
            tenantId, vehicles["U-01"].Id, "WO-1031", "Front brake inspection & measure",
            "Driver reported soft pedal on U-01; inspect front brakes and measure pad thickness.",
            WorkOrderPriority.High, WorkOrderSource.Manual, null, "Dispatch", "Thompson shop",
            now.AddDays(4), ["Inspect front brakes", "Measure pad thickness"],
            null, null, null, null).Value);

        context.WorkOrders.Add(WorkOrder.Create(
            tenantId, vehicles["U-03"].Id, "WO-1032", "Wiper motor intermittent",
            "DTC B1000 raised for wiper motor; wipers cut out intermittently.",
            WorkOrderPriority.Medium, WorkOrderSource.DtcAlert, "B1000", "Dispatch", "Thompson shop",
            null, ["Replace wiper motor", "Clear DTC B1000"], null, null, null, null).Value);

        // ---- Dispatcher-entered inspections (DVIR) ----
        context.VehicleInspections.Add(VehicleInspection.EnterByDispatcher(
            tenantId, "U-03", InspectionType.PreTrip, "L. Moose", "Dispatch · A. Klein",
            now.AddDays(-2), 98_750,
            StandardChecklist(("Windshield & wipers", false)),
            [new InspectionDefect { Item = "Wiper blade — driver side", Severity = InspectionDefectSeverity.Minor, Note = "Streaking; replace at next PM" }]).Value);
    }

    private static void AddDocument(FleetDbContext context, Guid tenantId, Guid vehicleId, string number, DocumentType type, string fileName, int sizeKb, DateTimeOffset? expiry) =>
        context.VehicleDocuments.Add(VehicleDocument.Add(tenantId, vehicleId, number, type, fileName, sizeKb, "MB Public Insurance", expiry, null).Value);

    private static readonly string[] StandardItems =
    [
        "Service brakes", "Parking brake", "Steering", "Lights & reflectors", "Tires & wheels",
        "Windshield & wipers", "Mirrors", "Horn", "Coupling / hitch", "Emergency equipment",
        "Seats & seatbelts", "Exhaust / fluid leaks",
    ];

    /// <summary>Standard NSC-11 checklist with the named items failed, the rest passed.</summary>
    private static List<InspectionChecklistItem> StandardChecklist(params (string Item, bool Passed)[] overrides)
    {
        var failed = overrides.Where(o => !o.Passed).Select(o => o.Item).ToHashSet();
        return StandardItems
            .Select(item => new InspectionChecklistItem { Item = item, Passed = !failed.Contains(item) })
            .ToList();
    }
}
