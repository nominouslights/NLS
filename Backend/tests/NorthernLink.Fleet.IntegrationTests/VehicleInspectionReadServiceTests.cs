using Npgsql;
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
    private static VehicleInspection Inspection(
        Guid tenantId,
        string tripNumber,
        string unit,
        IReadOnlyList<InspectionChecklistItem>? checklistItems = null,
        IReadOnlyList<InspectionDefect>? defects = null,
        string? certificationStatement = null) =>
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
            checklistItems: checklistItems ?? [],
            defects: defects ?? [],
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
            fuelCostCad: null,
            certificationStatement).Value;

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

    /// <summary>
    /// The NL-PTI-01 fields through a real projection round trip. This is the test that catches a
    /// missing <c>HasConversion&lt;string&gt;()</c> on the read-model side of the checklist jsonb:
    /// the enum lives inside a <c>ToJson</c> block, so a conversion configured on the write side
    /// only has the projector store <c>"NotApplicable"</c> in a document this reader deserializes
    /// as an int. Nothing in the build or in any in-memory test sees it — it throws in the
    /// projector at runtime, against real Postgres, which is exactly here.
    /// </summary>
    [Fact]
    public async Task The_tri_state_note_and_carrier_acknowledgement_survive_the_projection()
    {
        const string Statement = "I certify this vehicle was inspected per NSC Standard 13.";
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var trip = $"TC-{suffix}";
        Guid inspectionId;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var inspection = Inspection(
                PostgresFixture.TenantA,
                trip,
                "U-03",
                checklistItems: [
                    new InspectionChecklistItem { Group = "Exterior & Mechanical", Item = "Tires", State = ChecklistItemState.Ok },
                    new InspectionChecklistItem
                    {
                        Group = "Interior",
                        Item = "Wheelchair lift",
                        State = ChecklistItemState.NotApplicable,
                        Note = "Unit has no lift fitted",
                    },
                    new InspectionChecklistItem { Group = "Interior", Item = "Defroster", State = ChecklistItemState.Defect },
                ],
                defects: [new InspectionDefect { Item = "Defroster", Severity = InspectionDefectSeverity.Major, Note = "no output" }],
                certificationStatement: Statement);

            Assert.True(inspection
                .AcknowledgeAsCarrier("R. Beardy", "Unit parked pending repair", DateTimeOffset.UtcNow)
                .IsSuccess);

            writer.VehicleInspections.Add(inspection);
            await writer.SaveChangesAsync();
            inspectionId = inspection.Id;
        }

        await fixture.RebuildFleetProjectionsAsync();

        // The asymmetry guard, read off disk. Each table is self-consistent when only ONE side
        // carries HasConversion<string>() — the writer round-trips its own ints happily — so
        // asserting through the API contract alone cannot see the drift. What must hold is that
        // BOTH documents encode the state the same way, by name, which is what makes the write
        // table and its projection interchangeable to anything reading the jsonb directly.
        await using (var raw = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            foreach (var table in new[] { "fleet.vehicle_inspections", "fleet.rm_vehicle_inspections" })
            {
                await using var command = new NpgsqlCommand(
                    $"SELECT checklist_items::text FROM {table} WHERE id = @id;", raw);
                command.Parameters.AddWithValue("id", inspectionId);

                var document = (string)(await command.ExecuteScalarAsync())!;

                Assert.True(
                    document.Contains("\"NotApplicable\"", StringComparison.Ordinal),
                    $"{table}.checklist_items stores the tri-state as something other than its name "
                    + $"— add .Property(c => c.State).HasConversion<string>() to that side's mapping. Got: {document}");
            }
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var service = new VehicleInspectionReadService(reader);

        var only = Assert.Single(await service.GetInspectionsAsync(unit: null, tripNumber: trip));

        // The jsonb keys: the state comes back by NAME, and the per-item note comes back at all.
        var lift = only.Checklist.Single(c => c.Item == "Wheelchair lift");
        Assert.Equal("NotApplicable", lift.State);
        Assert.Equal("Unit has no lift fitted", lift.Note);
        Assert.True(lift.Passed);

        Assert.Equal("Ok", only.Checklist.Single(c => c.Item == "Tires").State);

        var defroster = only.Checklist.Single(c => c.Item == "Defroster");
        Assert.Equal("Defect", defroster.State);
        Assert.False(defroster.Passed);

        // …and the root columns the migration adds.
        Assert.Equal("Fail", only.Result);
        Assert.Equal("R. Beardy", only.CarrierAcknowledgedBy);
        Assert.NotNull(only.CarrierAcknowledgedAtUtc);
        Assert.Equal("Unit parked pending repair", only.CarrierAcknowledgementNote);
        Assert.Equal(Statement, only.CertificationStatement);
    }

    [Fact]
    public async Task A_legacy_checklist_row_still_projects_a_state()
    {
        // A row written in the pre-tri-state shape (Passed only, no State in the jsonb) must read
        // back through EffectiveState rather than as null — the backlog stays legible.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var trip = $"TD-{suffix}";

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.VehicleInspections.Add(Inspection(
                PostgresFixture.TenantA,
                trip,
                "U-05",
                checklistItems: [
                    new InspectionChecklistItem { Item = "Tires", Passed = true },
                    new InspectionChecklistItem { Item = "Defroster", Passed = false },
                ]));

            await writer.SaveChangesAsync();
        }

        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var service = new VehicleInspectionReadService(reader);

        var only = Assert.Single(await service.GetInspectionsAsync(unit: null, tripNumber: trip));

        Assert.Equal("Ok", only.Checklist.Single(c => c.Item == "Tires").State);
        Assert.Equal("Defect", only.Checklist.Single(c => c.Item == "Defroster").State);
        Assert.All(only.Checklist, c => Assert.Null(c.Note));
        Assert.Null(only.CarrierAcknowledgedBy);
        Assert.Null(only.CertificationStatement);
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
