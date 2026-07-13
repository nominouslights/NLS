using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Builds valid vehicles with unique unit numbers and VINs — the fleet schema enforces
/// per-tenant uniqueness on both, and every test shares one database.
/// </summary>
internal static class TestVehicleFactory
{
    // 15-char valid VIN stem + 2 digits from the counter = 17 (no I, O, Q — ISO 3779).
    private const string VinStem = "1HVBBAAN8RH5533";

    private static int _counter;

    public static Vehicle Create(Guid tenantId, int odometerKm = 100_000, int endOfLifeKm = 500_000)
    {
        var sequence = Interlocked.Increment(ref _counter);
        var result = Vehicle.Register(
            tenantId,
            unitNumber: $"U-{sequence:D3}",
            Vin.Create($"{VinStem}{sequence % 100:D2}").Value,
            make: "International",
            model: "3000",
            year: 2019,
            seatingCapacity: 24,
            licencePlate: $"MB · GHT {sequence:D3}",
            requiredLicenceClass: "Class 4",
            odometerKm,
            acquisitionCostCad: 100_000m,
            endOfLifeKm);

        Assert.True(result.IsSuccess, $"Test vehicle registration failed: {result.Error.Code}");
        return result.Value;
    }
}
