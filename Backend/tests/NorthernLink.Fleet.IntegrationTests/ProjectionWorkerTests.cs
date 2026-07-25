using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        var checkpoint = await ReadCheckpointAsync();
        Assert.Equal(await ReadGlobalJournalMaxAsync(), checkpoint);
        Assert.True(checkpoint > 0);

        // No new journal rows since the first poll — the second poll must not move the cursor.
        await worker.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(checkpoint, await ReadCheckpointAsync());
    }

    [Fact]
    public async Task Staleness_predicate_flips_on_unprojected_mutation_and_clears_after_next_poll()
    {
        var worker = fixture.BuildFleetProjectionWorker();

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

    private async Task<long> ReadCheckpointAsync() => await ScalarUnderSystemAsync(
        "SELECT coalesce((SELECT last_position FROM fleet.projection_checkpoints WHERE projection_name = 'fleet'), 0);");

    private async Task<long> ReadGlobalJournalMaxAsync() => await ScalarUnderSystemAsync(
        "SELECT coalesce(max(position), 0) FROM fleet.event_journal;");

    private async Task<long> ScalarUnderSystemAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString);
        await connection.OpenAsync();
        await using (var system = new NpgsqlCommand("SELECT set_config('app.is_system', 'true', false);", connection))
        {
            await system.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
