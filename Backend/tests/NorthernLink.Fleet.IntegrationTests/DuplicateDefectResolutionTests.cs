using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Inspections.GetDuplicates;
using NorthernLink.Fleet.Application.Inspections.ResolveDefect;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence;
using NorthernLink.Shared.Persistence.Auditing;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Clearing duplicate defects against real Postgres, as the non-superuser app role. The unit
/// tests prove the rule; these prove the parts only a database can: that
/// <c>VehicleInspectionRepository.GetWithOpenDefectsAsync</c>'s jsonb predicate (an EXISTS over
/// <c>jsonb_to_recordset("defects")</c>, with NO item predicate) finds the right rows, that the
/// tenant filter and RLS keep another tenant's identical defect out of it, that the preview and
/// the cascade read the same thing, that the two saves in one request each journal exactly their
/// own aggregates, that a stale Version really surfaces as a lost race, and that
/// <c>ResolvedWithInspectionId</c> round-trips through the write-side jsonb, the projection and
/// the defects read service.
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

    private async Task SaveAsync(Guid tenantId, params VehicleInspection[] inspections)
    {
        await using var writer = fixture.CreateContext(tenantId);
        writer.VehicleInspections.AddRange(inspections);
        await writer.SaveChangesAsync();
    }

    private static GetDefectDuplicatesQueryHandler Preview(FleetDbContext context) =>
        new(new VehicleInspectionRepository(context), TestDuplicateCascade.Over(context));

    private static ResolveInspectionDefectCommandHandler Resolve(FleetDbContext context) =>
        new(new VehicleInspectionRepository(context), TestDuplicateCascade.Over(context));

    [Fact]
    public async Task The_preview_lists_the_vehicles_open_duplicates_and_confirming_it_clears_exactly_those()
    {
        var vehicle = await SeedVehicleAsync();
        var otherVehicle = await SeedVehicleAsync();
        var day1 = DateTimeOffset.UtcNow.AddDays(-3);

        // A non-breaking space: .NET Trim() strips it, Postgres btrim() does not — the old
        // lower(btrim(item)) SQL predicate would have dropped this row. Item matching is now the
        // domain's rule only, in memory.
        var legacy = Inspection(PostgresFixture.TenantA, null, vehicle.UnitNumber, day1, Defect("MIRROR "));
        var linked = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddDays(1), Defect("Mirror"), Defect("Horn"));
        var addressed = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddDays(2), Defect("Mirror"));
        var alreadyCleared = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddHours(1), Defect("Mirror"));
        Assert.True(alreadyCleared
            .ResolveDefect("Mirror", DefectResolutionReason.ReportedInError, "glare", "Dispatch", day1.AddHours(2))
            .IsSuccess);
        var otherTruck = Inspection(PostgresFixture.TenantA, otherVehicle.Id, otherVehicle.UnitNumber, day1, Defect("Mirror"));
        // Linked to ANOTHER vehicle but carrying this vehicle's unit text: the link wins.
        var otherTruckSameText = Inspection(PostgresFixture.TenantA, otherVehicle.Id, vehicle.UnitNumber, day1, Defect("Mirror"));
        // Same vehicle id and item in ANOTHER tenant: the query filter and RLS must both keep it out.
        var foreign = Inspection(PostgresFixture.TenantB, vehicle.Id, vehicle.UnitNumber, day1, Defect("Mirror"));

        await SaveAsync(PostgresFixture.TenantA, legacy, linked, addressed, alreadyCleared, otherTruck, otherTruckSameText);
        await SaveAsync(PostgresFixture.TenantB, foreign);

        IReadOnlyList<DefectDuplicateResponse> shown;
        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var preview = await Preview(reader).Handle(
                new GetDefectDuplicatesQuery(PostgresFixture.TenantA, addressed.Id, " mirror"), CancellationToken.None);

            Assert.True(preview.IsSuccess, preview.IsFailure ? preview.Error.Code : null);
            shown = preview.Value.Duplicates;
            Assert.Equal([legacy.Id, linked.Id], shown.Select(d => d.InspectionId));
            Assert.Equal("MIRROR ", shown[0].Item);
            Assert.Equal("Major", shown[0].Severity);
            Assert.True((legacy.PerformedAt - shown[0].ReportedAt).Duration() < TimeSpan.FromMilliseconds(1));
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var result = await Resolve(writer).Handle(
                new ResolveInspectionDefectCommand(
                    PostgresFixture.TenantA, addressed.Id, "mirror", DefectResolutionReason.PreviouslyRepaired,
                    "new glass", "R. Ballantyne",
                    [.. shown.Select(d => new DuplicateDefectReference(d.InspectionId, d.Item))]),
                CancellationToken.None);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(
                [addressed.Id, .. shown.Select(d => d.InspectionId)],
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
            Assert.All(other, d => Assert.Null(d.ResolvedAtUtc));

            // Two saves in one request: each aggregate is journaled and snapshotted once per save
            // that touched it — never twice, never eventless.
            foreach (var id in new[] { addressed.Id, legacy.Id, linked.Id })
            {
                var versions = await reader.Set<AggregateSnapshot>()
                    .Where(s => s.AggregateId == id).Select(s => s.Version).OrderBy(v => v).ToListAsync();
                Assert.Equal([1, 2], versions);

                var journal = await reader.Set<EventJournalEntry>()
                    .Where(e => e.AggregateId == id).Select(e => e.AggregateVersion).Distinct().OrderBy(v => v).ToListAsync();
                Assert.Equal([1, 2], journal);
            }
        }

        await using (var readerB = fixture.CreateContext(PostgresFixture.TenantB))
        {
            var stored = await readerB.VehicleInspections.FindAsync(foreign.Id);
            Assert.False(Assert.Single(stored!.Defects).IsResolved);
        }
    }

    [Fact]
    public async Task The_preview_is_404_for_an_unknown_item_and_empty_for_a_resolved_defect()
    {
        var vehicle = await SeedVehicleAsync();
        var day1 = DateTimeOffset.UtcNow.AddDays(-2);
        var duplicate = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1, Defect("Mirror"));
        var resolved = Inspection(PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, day1.AddDays(1), Defect("Mirror"));
        Assert.True(resolved
            .ResolveDefect("Mirror", DefectResolutionReason.AcceptedMonitoring, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        await SaveAsync(PostgresFixture.TenantA, duplicate, resolved);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);

        var unknownItem = await Preview(reader).Handle(
            new GetDefectDuplicatesQuery(PostgresFixture.TenantA, resolved.Id, "Horn"), CancellationToken.None);
        Assert.Equal(InspectionErrors.DefectNotFound, unknownItem.Error);

        var unknownInspection = await Preview(reader).Handle(
            new GetDefectDuplicatesQuery(PostgresFixture.TenantA, Guid.NewGuid(), "Mirror"), CancellationToken.None);
        Assert.Equal(InspectionErrors.NotFound, unknownInspection.Error);

        var none = await Preview(reader).Handle(
            new GetDefectDuplicatesQuery(PostgresFixture.TenantA, resolved.Id, "Mirror"), CancellationToken.None);
        Assert.Empty(none.Value.Duplicates);
    }

    [Fact]
    public async Task A_stale_version_is_a_lost_race_that_persists_nothing_and_clears_the_tracker()
    {
        var vehicle = await SeedVehicleAsync();
        var inspection = Inspection(
            PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, DateTimeOffset.UtcNow.AddDays(-1), Defect("Mirror"), Defect("Horn"));
        await SaveAsync(PostgresFixture.TenantA, inspection);

        await using var loser = fixture.CreateContext(PostgresFixture.TenantA);
        var loserRepository = new VehicleInspectionRepository(loser);
        var stale = Assert.Single(await loserRepository.GetWithOpenDefectsAsync(DefectVehicleScope.ForVehicle(vehicle.Id, vehicle.UnitNumber)));

        // Another request wins the race on the same row.
        await using (var winner = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var fresh = await winner.VehicleInspections.SingleAsync(i => i.Id == inspection.Id);
            Assert.True(fresh.ResolveDefect("Horn", DefectResolutionReason.PreviouslyRepaired, null, "Other", DateTimeOffset.UtcNow).IsSuccess);
            await winner.SaveChangesAsync();
        }

        Assert.True(stale.ResolveDefect("Mirror", DefectResolutionReason.PreviouslyRepaired, null, "Loser", DateTimeOffset.UtcNow).IsSuccess);

        Assert.False(await loserRepository.TrySaveChangesAsync());
        Assert.Empty(loser.ChangeTracker.Entries());

        // A retry reads the winner's state, applies again, and saves at the new version.
        var retried = Assert.Single(await loserRepository.GetWithOpenDefectsAsync(DefectVehicleScope.ForVehicle(vehicle.Id, vehicle.UnitNumber)));
        Assert.True(retried.FindDefect("Horn")!.IsResolved);
        Assert.True(retried.ResolveDefect("Mirror", DefectResolutionReason.PreviouslyRepaired, null, "Loser", DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(await loserRepository.TrySaveChangesAsync());

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var versions = await reader.Set<AggregateSnapshot>()
            .Where(s => s.AggregateId == inspection.Id).Select(s => s.Version).OrderBy(v => v).ToListAsync();
        // Entry, the winner, the retry — the lost attempt left no snapshot behind.
        Assert.Equal([1, 2, 3], versions);
    }

    [Fact]
    public async Task A_unit_only_scope_queries_only_unlinked_inspections_with_that_unit()
    {
        var vehicle = await SeedVehicleAsync();
        var unit = $"LEG-{Guid.NewGuid():N}"[..12];
        var day = DateTimeOffset.UtcNow.AddDays(-1);
        var legacy = Inspection(PostgresFixture.TenantA, null, unit, day, Defect("Mirror"));
        var legacyAllResolved = Inspection(PostgresFixture.TenantA, null, unit, day, Defect("Mirror"));
        Assert.True(legacyAllResolved
            .ResolveDefect("Mirror", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", day)
            .IsSuccess);
        var linkedSameText = Inspection(PostgresFixture.TenantA, vehicle.Id, unit, day, Defect("Mirror"));
        await SaveAsync(PostgresFixture.TenantA, legacy, legacyAllResolved, linkedSameText);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var found = await new VehicleInspectionRepository(reader).GetWithOpenDefectsAsync(DefectVehicleScope.UnitOnly(unit));

        Assert.Equal(legacy.Id, Assert.Single(found).Id);
    }
}
