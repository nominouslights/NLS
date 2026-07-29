using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Exercises the real read-side <c>WHERE TripNumber == @trip</c> filter against a real Postgres
/// read model (the projector-maintained rm table) as the non-superuser app role. This is the SQL
/// the bug fix added: before it, <c>GetInspectionsAsync</c> ignored the trip number and every
/// trip's detail view showed every inspection.
/// </summary>
[Collection("postgres")]
public class VehicleInspectionReadServiceTests(PostgresFixture fixture)
{
    private static VehicleInspection Inspection(Guid tenantId, string tripNumber, string unit) =>
        VehicleInspection.Enter(
            tenantId,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            tripNumber,
            vehicleId: null,
            unit,
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm: 118_204,
            checklistItems: [],
            defects: [],
            weather: [],
            temperatureC: null,
            roadConditions: [],
            visibility: null,
            roadAdvisories: null,
            fuelLevel: null,
            issues: [],
            attestations: [],
            driverSignatureName: null,
            certifiedAt: null,
            fuelAdded: false,
            fuelLitres: null,
            fuelCostCad: null).Value;

    [Fact]
    public async Task Trip_number_filter_returns_only_that_trips_inspections()
    {
        // TripNumber is varchar(32) — keep the unique suffix short.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tripA = $"TA-{suffix}";
        var tripB = $"TB-{suffix}";

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.VehicleInspections.Add(Inspection(PostgresFixture.TenantA, tripA, "U-01"));
            writer.VehicleInspections.Add(Inspection(PostgresFixture.TenantA, tripB, "U-02"));
            await writer.SaveChangesAsync();
        }

        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var service = new VehicleInspectionReadService(reader);

        var forTripA = await service.GetInspectionsAsync(unit: null, tripNumber: tripA);

        var only = Assert.Single(forTripA);
        Assert.Equal(tripA, only.TripNumber);
    }

    [Fact]
    public async Task No_trip_number_returns_every_tenant_inspection()
    {
        // TripNumber is varchar(32) — keep the unique suffix short.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tripA = $"TA-{suffix}";
        var tripB = $"TB-{suffix}";

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantB))
        {
            writer.VehicleInspections.Add(Inspection(PostgresFixture.TenantB, tripA, "U-01"));
            writer.VehicleInspections.Add(Inspection(PostgresFixture.TenantB, tripB, "U-02"));
            await writer.SaveChangesAsync();
        }

        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(PostgresFixture.TenantB);
        var service = new VehicleInspectionReadService(reader);

        var all = await service.GetInspectionsAsync(unit: null, tripNumber: null);

        Assert.Contains(all, i => i.TripNumber == tripA);
        Assert.Contains(all, i => i.TripNumber == tripB);
    }
}
