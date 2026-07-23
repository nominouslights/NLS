using Npgsql;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Exercises the database half of the platform's dual enforcement with raw SQL as the
/// non-superuser app role — no EF query filters in the way. These are the guarantees
/// local dev (superuser) can never verify.
/// </summary>
[Collection("postgres")]
public class RowLevelSecurityTests(PostgresFixture fixture)
{
    private async Task<Vehicle> SeedTenantAVehicleAsync()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);
        await using var context = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        Assert.True(vehicle.ChangeStatus(VehicleStatus.OutOfService, "RLS test").IsSuccess);
        await context.SaveChangesAsync();
        return vehicle;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql, Guid aggregateId)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", aggregateId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, Guid aggregateId)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", aggregateId);
        return await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Other_tenants_see_no_audit_rows()
    {
        var vehicle = await SeedTenantAVehicleAsync();

        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.True(await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.event_journal WHERE aggregate_id = @id", vehicle.Id) >= 2);
            Assert.True(await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.aggregate_snapshots WHERE aggregate_id = @id", vehicle.Id) >= 2);
        }

        await using var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB);
        Assert.Equal(0, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.event_journal WHERE aggregate_id = @id", vehicle.Id));
        Assert.Equal(0, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.aggregate_snapshots WHERE aggregate_id = @id", vehicle.Id));
    }

    [Fact]
    public async Task Journal_and_snapshots_are_append_only_even_for_the_owning_tenant()
    {
        var vehicle = await SeedTenantAVehicleAsync();

        await using var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA);

        // No UPDATE/DELETE policies exist: with RLS forced, these statements match 0 rows.
        Assert.Equal(0, await ExecuteAsync(tenantA,
            "UPDATE fleet.event_journal SET event_type = 'tampered' WHERE aggregate_id = @id", vehicle.Id));
        Assert.Equal(0, await ExecuteAsync(tenantA,
            "DELETE FROM fleet.event_journal WHERE aggregate_id = @id", vehicle.Id));
        Assert.Equal(0, await ExecuteAsync(tenantA,
            "UPDATE fleet.aggregate_snapshots SET state = '{}' WHERE aggregate_id = @id", vehicle.Id));
        Assert.Equal(0, await ExecuteAsync(tenantA,
            "DELETE FROM fleet.aggregate_snapshots WHERE aggregate_id = @id", vehicle.Id));
    }

    [Fact]
    public async Task Outbox_rows_are_invisible_without_tenant_or_system_session()
    {
        var vehicle = await SeedTenantAVehicleAsync();
        const string touchSql =
            "UPDATE fleet.outbox_messages SET attempts = attempts WHERE payload::text LIKE '%' || (@id)::text || '%'";

        // No tenant, no system flag: the dispatcher's UPDATE would silently do nothing.
        await using (var anonymous = await fixture.OpenRawConnectionAsync())
        {
            Assert.Equal(0, await ExecuteAsync(anonymous, touchSql, vehicle.Id));
        }

        // The system session variable is exactly what lets the outbox dispatcher through.
        await using var system = await fixture.OpenRawConnectionAsync();
        await using (var enable = new NpgsqlCommand(
            "SELECT set_config('app.is_system', 'true', false);", system))
        {
            await enable.ExecuteNonQueryAsync();
        }

        Assert.True(await ExecuteAsync(system, touchSql, vehicle.Id) >= 1);
    }

    // ---- Read-side projection tables: native RLS, same policy idiom as the write tables ----
    // The old two-role backstop (projector-owned matview + security-barrier wrapper view) is
    // gone; fleet.rm_* are ordinary tables the app role owns, isolated by a row policy that
    // FORCE ROW LEVEL SECURITY binds even for the owner.

    [Fact]
    public async Task Projection_rebuild_captures_all_tenants_but_rls_isolates_reads()
    {
        var vehicleA = TestVehicleFactory.Create(PostgresFixture.TenantA);
        var vehicleB = TestVehicleFactory.Create(PostgresFixture.TenantB);
        await using (var contextA = fixture.CreateContext(PostgresFixture.TenantA))
        {
            contextA.Vehicles.Add(vehicleA);
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = fixture.CreateContext(PostgresFixture.TenantB))
        {
            contextB.Vehicles.Add(vehicleB);
            await contextB.SaveChangesAsync();
        }

        // The rebuild runs on a system session (app.is_system), so it writes BOTH tenants' rows.
        await fixture.RebuildFleetProjectionsAsync();

        // A tenant-scoped session only ever sees its own rows — enforced by the row policy now,
        // not by a wrapper view.
        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.rm_vehicles WHERE id = @id", vehicleA.Id));
            Assert.Equal(0, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.rm_vehicles WHERE id = @id", vehicleB.Id));
        }

        await using var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB);
        Assert.Equal(1, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.rm_vehicles WHERE id = @id", vehicleB.Id));
        Assert.Equal(0, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.rm_vehicles WHERE id = @id", vehicleA.Id));
    }

    [Fact]
    public async Task Anonymous_session_sees_zero_projection_rows_without_a_22P02()
    {
        var vehicle = await SeedTenantAVehicleAsync();
        await fixture.RebuildFleetProjectionsAsync();

        // No tenant set: the NULLIF guard turns the (possibly empty-string) GUC into NULL, so
        // the policy cleanly matches no rows instead of throwing 22P02 on an empty-string cast.
        await using var anonymous = await fixture.OpenRawConnectionAsync();
        Assert.Equal(0, await CountAsync(anonymous,
            "SELECT count(*) FROM fleet.rm_vehicles WHERE id = @id", vehicle.Id));

        await using var all = new NpgsqlCommand("SELECT count(*) FROM fleet.rm_vehicles", anonymous);
        Assert.Equal(0L, (long)(await all.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Projector_role_is_no_longer_required_by_any_migration()
    {
        // The fixture provisions northernlink_app only. If any migration still granted to or
        // set role to northernlink_projector, migrating this database would have failed with
        // 42704 during fixture startup — so reaching this assertion at all is the real proof.
        await using var connection = await fixture.OpenRawConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_roles WHERE rolname = 'northernlink_projector'", connection);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }
}
