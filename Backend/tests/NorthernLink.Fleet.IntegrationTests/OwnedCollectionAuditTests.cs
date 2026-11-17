using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Infrastructure.Persistence;
using NorthernLink.Shared.Persistence.Auditing;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The shared audit pipeline (<c>ModuleDbContext</c>) against a save whose ONLY change is to an
/// owned jsonb collection — <c>VehicleInspection.Defects</c>, mapped
/// <c>OwnsMany(...).ToJson("defects")</c>. EF tracks the owned items as Added/Deleted and leaves the
/// owning aggregate Unchanged while still rewriting its row; before the pipeline promoted that owner,
/// such a write skipped the version check, the snapshot, the journal (so the journal-driven read
/// model never learned) and the eventless-write guard. VehicleInspection is only the vehicle here:
/// nothing in the behaviour under test is Fleet-specific.
/// </summary>
[Collection("postgres")]
public class OwnedCollectionAuditTests(PostgresFixture fixture)
{
    private static VehicleInspection Inspection(Guid vehicleId, string unit, params string[] defectItems) =>
        VehicleInspection.Enter(
            PostgresFixture.TenantA,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            tripNumber: null,
            vehicleId,
            unit,
            driverName: "J. Spence",
            enteredBy: null,
            DateTimeOffset.UtcNow.AddDays(-1),
            odometerKm: 118_204,
            checklistItems: [],
            [.. defectItems.Select(item => new InspectionDefect { Item = item, Severity = InspectionDefectSeverity.Major })],
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

    private async Task<VehicleInspection> SeedAsync(params string[] defectItems)
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);
        var inspection = Inspection(vehicle.Id, vehicle.UnitNumber, defectItems);

        await using var writer = fixture.CreateContext(PostgresFixture.TenantA);
        writer.Vehicles.Add(vehicle);
        writer.VehicleInspections.Add(inspection);
        await writer.SaveChangesAsync();

        return inspection;
    }

    [Fact]
    public async Task An_owned_collection_only_change_is_versioned_snapshotted_journaled_and_projected()
    {
        var seeded = await SeedAsync("Mirror", "Horn");
        await fixture.DrainFleetProjectionsAsync();

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var loaded = await writer.VehicleInspections.SingleAsync(i => i.Id == seeded.Id);
            Assert.True(loaded.ResolveDefect("Mirror", DefectResolutionReason.PreviouslyRepaired, "new glass", "Dispatch", DateTimeOffset.UtcNow).IsSuccess);

            // The premise: the owned items changed, EF still calls the owner Unchanged.
            writer.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Unchanged, writer.Entry(loaded).State);
            Assert.Contains(writer.ChangeTracker.Entries<InspectionDefect>(), e => e.State != EntityState.Unchanged);

            await writer.SaveChangesAsync();
            Assert.Equal(2, loaded.Version);
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);

        var stored = await reader.VehicleInspections.AsNoTracking().SingleAsync(i => i.Id == seeded.Id);
        Assert.Equal(2, stored.Version);
        Assert.True(stored.FindDefect("Mirror")!.IsResolved);

        var snapshots = await reader.Set<AggregateSnapshot>()
            .Where(s => s.AggregateId == seeded.Id).OrderBy(s => s.Version).ToListAsync();
        Assert.Equal([1, 2], snapshots.Select(s => s.Version));
        Assert.Contains("new glass", snapshots[1].State);

        var journal = await reader.Set<EventJournalEntry>()
            .Where(e => e.AggregateId == seeded.Id).OrderBy(e => e.Position).ToListAsync();
        Assert.Equal(
            [
                AuditNames.ForEvent(typeof(VehicleInspectionCreatedDomainEvent)),
                AuditNames.ForEvent(typeof(VehicleInspectionDefectsResolvedDomainEvent)),
            ],
            journal.Select(e => e.EventType));
        Assert.Equal([1, 2], journal.Select(e => e.AggregateVersion));

        // The symptom that started this: the JOURNAL-driven read model (not a rebuild from the
        // write tables) must now see the resolution.
        await fixture.DrainFleetProjectionsAsync();
        var defects = await new VehicleDefectReadService(reader)
            .GetDefectsForVehicleAsync(stored.VehicleId!.Value, includeResolved: true);
        Assert.NotNull(defects.Single(d => d.Item == "Mirror").ResolvedAtUtc);
        Assert.Null(defects.Single(d => d.Item == "Horn").ResolvedAtUtc);
    }

    [Fact]
    public async Task A_stale_owned_collection_only_save_is_a_concurrency_conflict_and_persists_nothing()
    {
        var seeded = await SeedAsync("Mirror", "Horn");

        await using var loser = fixture.CreateContext(PostgresFixture.TenantA);
        var stale = await loser.VehicleInspections.SingleAsync(i => i.Id == seeded.Id);

        await using (var winner = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var fresh = await winner.VehicleInspections.SingleAsync(i => i.Id == seeded.Id);
            Assert.True(fresh.ResolveDefect("Horn", DefectResolutionReason.PreviouslyRepaired, null, "Winner", DateTimeOffset.UtcNow).IsSuccess);
            await winner.SaveChangesAsync();
        }

        Assert.True(stale.ResolveDefect("Mirror", DefectResolutionReason.PreviouslyRepaired, null, "Loser", DateTimeOffset.UtcNow).IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loser.SaveChangesAsync());

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var stored = await reader.VehicleInspections.AsNoTracking().SingleAsync(i => i.Id == seeded.Id);
        Assert.Equal(2, stored.Version);
        Assert.True(stored.FindDefect("Horn")!.IsResolved);
        Assert.False(stored.FindDefect("Mirror")!.IsResolved);

        Assert.Equal(
            [1, 2],
            await reader.Set<AggregateSnapshot>()
                .Where(s => s.AggregateId == seeded.Id).Select(s => s.Version).OrderBy(v => v).ToListAsync());
        Assert.DoesNotContain(
            await reader.Set<EventJournalEntry>().Where(e => e.AggregateId == seeded.Id).ToListAsync(),
            e => e.Payload.Contains("Loser"));
    }

    [Fact]
    public async Task The_synchronous_save_path_reports_the_same_lost_race()
    {
        var seeded = await SeedAsync("Mirror", "Horn");

        using var loser = fixture.CreateContext(PostgresFixture.TenantA);
        var stale = loser.VehicleInspections.Single(i => i.Id == seeded.Id);

        using (var winner = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var fresh = winner.VehicleInspections.Single(i => i.Id == seeded.Id);
            Assert.True(fresh.ResolveDefect("Horn", DefectResolutionReason.PreviouslyRepaired, null, "Winner", DateTimeOffset.UtcNow).IsSuccess);
            winner.SaveChanges();
        }

        Assert.True(stale.ResolveDefect("Mirror", DefectResolutionReason.PreviouslyRepaired, null, "Loser", DateTimeOffset.UtcNow).IsSuccess);
        Assert.Throws<DbUpdateConcurrencyException>(() => loser.SaveChanges());
    }

    [Fact]
    public async Task An_eventless_owned_collection_only_change_trips_the_guard_and_writes_nothing()
    {
        var seeded = await SeedAsync("Mirror");

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var loaded = await writer.VehicleInspections.SingleAsync(i => i.Id == seeded.Id);

            // Bypass the aggregate's methods: change the owned item and raise nothing.
            loaded.Defects[0] = loaded.Defects[0] with { Note = "edited behind the aggregate's back" };

            var guard = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            Assert.Contains("without raising a domain event", guard.Message);
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var stored = await reader.VehicleInspections.AsNoTracking().SingleAsync(i => i.Id == seeded.Id);
        Assert.Equal(1, stored.Version);
        Assert.Null(stored.FindDefect("Mirror")!.Note);
    }
}
