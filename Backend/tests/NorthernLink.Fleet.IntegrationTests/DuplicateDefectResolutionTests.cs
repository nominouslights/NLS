using NorthernLink.Fleet.Application.Inspections.ResolveDefect;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Clearing duplicate defects against real Postgres, as the non-superuser app role. The unit
/// tests prove the rule; these prove the parts only a database can: that
/// <c>VehicleInspectionRepository.GetWithOpenDefectAsync</c>'s jsonb predicate (an EXISTS over
/// <c>jsonb_to_recordset("defects")</c>) finds the right rows, that the tenant filter and RLS keep
/// another tenant's identical defect out of it, and that <c>ResolvedWithInspectionId</c>
/// round-trips through the write-side jsonb, the projection and the defects read service.
/// </summary>
[Collection("postgres")]
public class DuplicateDefectResolutionTests(PostgresFixture fixture)
{
    private static InspectionDefect Defect(string item) =>
        new() { Item = item, Severity = InspectionDefectSeverity.Major, Note = "as found" };

    private static VehicleInspection Inspection(
        Guid tenantId, Guid? vehicleId, string unit, DateTimeOffset performedAt, params InspectionDefect[] defects) =>
        VehicleInspection.Enter(
            tenantId,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            tripNumber: null,
            vehicleId,
            unit,
            driverName: "J. Spence",
            enteredBy: null,
            performedAt,
            odometerKm: 118_204,
            checklistItems: [],
            defects,
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

    private async Task<Vehicle> SeedVehicleAsync()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);

        await using var writer = fixture.CreateContext(PostgresFixture.TenantA);
        writer.Vehicles.Add(vehicle);
        await writer.SaveChangesAsync();

        return vehicle;
    }

    [Fact]
    public async Task Resolving_one_report_clears_the_vehicles_open_duplicates_and_nothing_else()
    {
        var vehicle = await SeedVehicleAsync();
        var otherVehicle = await SeedVehicleAsync();
        var day1 = DateTimeOffset.UtcNow.AddDays(-3);

        var legacy = Inspection(PostgresFixture.TenantA, null, vehicle.UnitNumber, day1, Defect("  MIRROR "));
        var linked = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddDays(1), Defect("Mirror"), Defect("Horn"));
        var addressed = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddDays(2), Defect("Mirror"));
        var alreadyCleared = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddHours(1), Defect("Mirror"));
        Assert.True(alreadyCleared
            .ResolveDefect("Mirror", DefectResolutionReason.ReportedInError, "glare", "Dispatch", day1.AddHours(2))
            .IsSuccess);
        var otherTruck = Inspection(PostgresFixture.TenantA, otherVehicle.Id, otherVehicle.UnitNumber, day1, Defect("Mirror"));
        // Same vehicle id and item in ANOTHER tenant: the query filter and RLS must both keep it out.
        var foreign = Inspection(PostgresFixture.TenantB, vehicle.Id, vehicle.UnitNumber, day1, Defect("Mirror"));

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.VehicleInspections.AddRange(legacy, linked, addressed, alreadyCleared, otherTruck);
            await writer.SaveChangesAsync();
        }

        await using (var writerB = fixture.CreateContext(PostgresFixture.TenantB))
        {
            writerB.VehicleInspections.Add(foreign);
            await writerB.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var handler = new ResolveInspectionDefectCommandHandler(
                new VehicleInspectionRepository(writer), new VehicleRepository(writer));

            var result = await handler.Handle(
                new ResolveInspectionDefectCommand(
                    PostgresFixture.TenantA, addressed.Id, "mirror", DefectResolutionReason.PreviouslyRepaired,
                    "new glass", "R. Ballantyne"),
                CancellationToken.None);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(
                [addressed.Id, legacy.Id, linked.Id],
                result.Value.Resolved.Select(r => r.InspectionId));
        }

        await fixture.RebuildFleetProjectionsAsync();

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var all = await new VehicleDefectReadService(reader)
                .GetDefectsForVehicleAsync(vehicle.Id, includeResolved: true);

            Assert.Null(all.Single(d => d.InspectionId == addressed.Id).ResolvedWithInspectionId);
            Assert.All(
                all.Where(d => d.InspectionId == legacy.Id || (d.InspectionId == linked.Id && d.Item == "Mirror")),
                d =>
                {
                    Assert.Equal(addressed.Id, d.ResolvedWithInspectionId);
                    Assert.Equal("PreviouslyRepaired", d.ResolutionReason);
                    Assert.Equal("new glass", d.ResolutionNote);
                });

            Assert.Null(all.Single(d => d.Item == "Horn").ResolvedAtUtc);
            var cleared = all.Single(d => d.InspectionId == alreadyCleared.Id);
            Assert.Equal("ReportedInError", cleared.ResolutionReason);
            Assert.Null(cleared.ResolvedWithInspectionId);

            var other = await new VehicleDefectReadService(reader).GetDefectsForVehicleAsync(otherVehicle.Id, includeResolved: true);
            Assert.Null(Assert.Single(other).ResolvedAtUtc);
        }

        await using (var readerB = fixture.CreateContext(PostgresFixture.TenantB))
        {
            var stored = await readerB.VehicleInspections.FindAsync(foreign.Id);
            Assert.False(Assert.Single(stored!.Defects).IsResolved);
        }
    }
}
