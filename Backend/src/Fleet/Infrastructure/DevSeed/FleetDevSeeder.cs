using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence;

namespace NorthernLink.Fleet.Infrastructure.DevSeed;

/// <summary>
/// Development-only seed: the six vehicles the Dispatcher's mock data
/// (Dispatcher/lib/data.ts) shows, so the console has real API data out of the box.
/// U-01 is deliberately at 93.6% of its service life so the depreciation / near-EOL UI
/// is visible immediately. Idempotent — skips when any vehicle already exists.
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

            context.Vehicles.Add(vehicle);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
