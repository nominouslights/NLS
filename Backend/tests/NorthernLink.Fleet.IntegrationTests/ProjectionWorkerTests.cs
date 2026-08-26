using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Drives the real <see cref="Shared.Persistence.Projections.ProjectionWorker{T}"/> one poll
/// at a time (no timer) against a real Postgres as the non-superuser app role: a command
/// appends a journal row, one poll upserts the read-model row and advances the checkpoint, a
/// second poll is a no-op, a deleted aggregate takes its read row with it, and the version-based
/// staleness predicate flips on an un-projected mutation and clears after the next poll.
/// </summary>
[Collection("postgres")]
public class ProjectionWorkerTests(PostgresFixture fixture)
{
    [Fact]
    public async Task One_poll_projects_the_vehicle_and_advances_the_checkpoint_then_second_poll_is_a_noop()
    {
        var worker = fixture.BuildFleetProjectionWorker();

        // Other test classes in this collection can leave the journal AHEAD of the checkpoint:
        // a poll's secondary commands (odometer propagation, retirement certificates) run after
        // that poll's checkpoint write, and any aggregate they mutate appends new journal rows
        // the finished poll does not cover. Drain to quiescence first so the strict
        // "checkpoint == max(position)" and "second poll is a no-op" assertions below measure
        // only this test's own append, with no implicit cross-class ordering invariant.
        await fixture.DrainFleetProjectionsAsync();

        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA, odometerKm: 100_000, endOfLifeKm: 500_000);
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Vehicles.Add(vehicle);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var projected = await reader.VehicleReadModels.SingleAsync(v => v.Id == vehicle.Id);
            Assert.Equal(vehicle.Version, projected.Version);
            Assert.Equal(vehicle.CurrentValueCad, projected.CurrentValueCad);
            Assert.Equal(vehicle.RemainingKm, projected.RemainingKm);
            Assert.Equal(vehicle.LifeUsedPct, projected.LifeUsedPct);
        }

        // Registering a vehicle triggers no OnEvent reaction (it is nowhere near end of life),
        // so after the drained baseline this poll's checkpoint lands exactly on the journal head.
        var checkpoint = await fixture.ReadFleetProjectionCheckpointAsync();
        Assert.Equal(await fixture.ReadFleetJournalHeadAsync(), checkpoint);
        Assert.True(checkpoint > 0);

        // No new journal rows since the first poll — the second poll must not move the cursor.
        await worker.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(checkpoint, await fixture.ReadFleetProjectionCheckpointAsync());
    }

    [Fact]
    public async Task Staleness_predicate_flips_on_unprojected_mutation_and_clears_after_next_poll()
    {
        var worker = fixture.BuildFleetProjectionWorker();

        // Drained baseline: with no backlog, each single poll below is guaranteed to reach
        // this test's own journal rows (one poll takes at most BatchSize rows).
        await fixture.DrainFleetProjectionsAsync();

        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA, odometerKm: 100_000, endOfLifeKm: 500_000);
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Vehicles.Add(vehicle);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);
        Assert.False(await IsStaleAsync(vehicle.Id));

        // Mutate the aggregate WITHOUT running the worker: a new journal row is appended at a
        // higher aggregate_version than the read row captured, so the predicate reports stale.
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Vehicles.SingleAsync(v => v.Id == vehicle.Id);
            Assert.True(tracked.ChangeStatus(VehicleStatus.OutOfService, "Staleness probe").IsSuccess);
            await writer.SaveChangesAsync();
        }

        Assert.True(await IsStaleAsync(vehicle.Id));

        await worker.ProcessOnceAsync(CancellationToken.None);
        Assert.False(await IsStaleAsync(vehicle.Id));
    }

    [Fact]
    public async Task Deleting_the_aggregate_removes_its_read_row_on_the_next_poll()
    {
        var worker = fixture.BuildFleetProjectionWorker();

        // Same drained baseline as above — single polls must reach this test's rows.
        await fixture.DrainFleetProjectionsAsync();

        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Vehicles.Add(vehicle);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            Assert.True(await reader.VehicleReadModels.AnyAsync(v => v.Id == vehicle.Id));
        }

        // Hard-delete the aggregate. The audit pipeline still journals the delete, so the next
        // poll finds no source row and drops the projection — something a REFRESH got for free
        // and targeted upserts have to handle explicitly.
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Vehicles.SingleAsync(v => v.Id == vehicle.Id);
            writer.Vehicles.Remove(tracked);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            Assert.False(await reader.VehicleReadModels.AnyAsync(v => v.Id == vehicle.Id));
        }
    }

    /// <summary>
    /// A read row is stale when the journal holds a higher aggregate_version than the row
    /// captured. Computed as two tenant reads (the version via rm_vehicles, the journal max via
    /// event_journal) — both now readable by the app role under its own tenant's row policy.
    /// </summary>
    private async Task<bool> IsStaleAsync(Guid vehicleId)
    {
        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var matviewVersion = await reader.VehicleReadModels
            .Where(v => v.Id == vehicleId)
            .Select(v => v.Version)
            .SingleAsync();
        var journalMaxVersion = await reader.Set<EventJournalEntry>()
            .Where(e => e.AggregateId == vehicleId)
            .MaxAsync(e => e.AggregateVersion);
        return journalMaxVersion > matviewVersion;
    }
}
