using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

[Collection("postgres")]
public class AuditPipelineTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Register_writes_journal_row_and_snapshot_in_same_transaction()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);

        await using (var context = fixture.CreateContext(PostgresFixture.TenantA))
        {
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);

        var journalEntry = Assert.Single(await reader.Set<EventJournalEntry>()
            .Where(e => e.AggregateId == vehicle.Id)
            .ToListAsync());
        Assert.Equal("vehicle-registered", journalEntry.EventType);
        Assert.Equal("vehicle", journalEntry.AggregateType);
        Assert.Equal(1, journalEntry.AggregateVersion);
        Assert.Equal(PostgresFixture.TenantA, journalEntry.TenantId);

        var snapshot = Assert.Single(await reader.Set<AggregateSnapshot>()
            .Where(s => s.AggregateId == vehicle.Id)
            .ToListAsync());
        Assert.Equal(1, snapshot.Version);
        Assert.Equal(PostgresFixture.TenantA, snapshot.TenantId);

        using var state = JsonDocument.Parse(snapshot.State);
        Assert.Equal(vehicle.UnitNumber, state.RootElement.GetProperty("unitNumber").GetString());
        Assert.Equal("Active", state.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, state.RootElement.GetProperty("version").GetInt32());
        Assert.False(state.RootElement.TryGetProperty("domainEvents", out _));
    }

    [Fact]
    public async Task Every_save_appends_a_snapshot_and_journal_rows_link_to_their_version()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);

        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();                                   // v1: registered

        Assert.True(vehicle.ChangeStatus(VehicleStatus.InMaintenance, "Brakes").IsSuccess);
        await context.SaveChangesAsync();                                   // v2: status-changed

        Assert.True(vehicle.RecordOdometer(120_000).IsSuccess);
        await context.SaveChangesAsync();                                   // v3: no event — snapshot only

        var snapshots = await context.Set<AggregateSnapshot>()
            .Where(s => s.AggregateId == vehicle.Id)
            .OrderBy(s => s.Version)
            .ToListAsync();
        Assert.Equal([1, 2, 3], snapshots.Select(s => s.Version));

        // The moment-in-time record: the event-less odometer change is still captured.
        using var v3 = JsonDocument.Parse(snapshots[2].State);
        Assert.Equal(120_000, v3.RootElement.GetProperty("odometerKm").GetInt32());

        var journal = await context.Set<EventJournalEntry>()
            .Where(e => e.AggregateId == vehicle.Id)
            .OrderBy(e => e.Position)
            .ToListAsync();
        Assert.Equal(["vehicle-registered", "vehicle-status-changed"], journal.Select(e => e.EventType));
        Assert.Equal([1, 2], journal.Select(e => e.AggregateVersion));
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_state_and_no_audit_rows()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);

        await using (var context = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();
            Assert.True(vehicle.ChangeStatus(VehicleStatus.OutOfService, "Rolled back anyway").IsSuccess);
            await context.SaveChangesAsync();                               // would write an outbox row
            await transaction.RollbackAsync();
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        Assert.False(await reader.Vehicles.AnyAsync(v => v.Id == vehicle.Id));
        Assert.False(await reader.Set<EventJournalEntry>().AnyAsync(e => e.AggregateId == vehicle.Id));
        Assert.False(await reader.Set<AggregateSnapshot>().AnyAsync(s => s.AggregateId == vehicle.Id));

        // The outbox row for the status change must have rolled back with everything else.
        // (Payload inspection client-side: other tests share this database's outbox table.)
        var tenantOutbox = await reader.Set<OutboxMessage>().ToListAsync();
        Assert.DoesNotContain(tenantOutbox, m => m.Payload.Contains(vehicle.Id.ToString()));
    }
}
