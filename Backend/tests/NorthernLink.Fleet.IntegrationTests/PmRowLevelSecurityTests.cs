using Npgsql;
using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Row-Level Security for the six preventative-maintenance tables, exercised with raw SQL as
/// the non-superuser app role (the guarantees local superuser dev can never prove):
/// tenant isolation on the write tables (maintenance_plans, pm_plan_assignments,
/// pm_completions) and their rm_* projections; the system session's asymmetry — read-only on
/// the write tables (narrow system_read policies), read-write on the rm_* tables (the
/// projection worker's two-arm policy); and pm_completions' append-only contract, which even
/// the owning tenant cannot update, delete, or forge another tenant's rows into.
/// </summary>
[Collection("postgres")]
public class PmRowLevelSecurityTests(PostgresFixture fixture)
{
    private sealed record PmSeed(Guid VehicleId, Guid PlanId, Guid AssignmentId, Guid CompletionId);

    private async Task<PmSeed> SeedPmRowsAsync(Guid tenantId)
    {
        var vehicle = TestVehicleFactory.Create(tenantId);
        var plan = PmTestData.Plan(
            tenantId,
            PmTestData.UniqueName("RLS plan"),
            [PmTestData.Item("PM-X-001", "Engine", "Engine oil & filter", 10_000, 6, 30)],
            []);
        var assignment = PmTestData.Assign(tenantId, vehicle.Id, plan.Id);
        var completion = PmTestData.Completion(
            tenantId, vehicle.Id, plan.Id, "PM-X-001", PmEntryKind.Item, PmSchedule.TodayUtc(), 99_000);

        await using var context = fixture.CreateContext(tenantId);
        context.Vehicles.Add(vehicle);
        context.MaintenancePlans.Add(plan);
        context.PlanAssignments.Add(assignment);
        context.PmCompletions.Add(completion);
        await context.SaveChangesAsync();

        return new PmSeed(vehicle.Id, plan.Id, assignment.Id, completion.Id);
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task EnableSystemSessionAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.is_system', 'true', false);", connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Pm_write_tables_are_invisible_and_unmodifiable_to_other_tenants()
    {
        var seed = await SeedPmRowsAsync(PostgresFixture.TenantA);

        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.maintenance_plans WHERE id = @id", seed.PlanId));
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.pm_plan_assignments WHERE id = @id", seed.AssignmentId));
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.pm_completions WHERE id = @id", seed.CompletionId));
        }

        await using var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB);
        Assert.Equal(0, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.maintenance_plans WHERE id = @id", seed.PlanId));
        Assert.Equal(0, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.pm_plan_assignments WHERE id = @id", seed.AssignmentId));
        Assert.Equal(0, await CountAsync(tenantB,
            "SELECT count(*) FROM fleet.pm_completions WHERE id = @id", seed.CompletionId));

        // Modification attempts from the wrong tenant match zero rows — not an error, just
        // nothing to touch, exactly how the policy USING arm is supposed to read.
        Assert.Equal(0, await ExecuteAsync(tenantB,
            "UPDATE fleet.maintenance_plans SET notes = 'tampered' WHERE id = @id", seed.PlanId));
        Assert.Equal(0, await ExecuteAsync(tenantB,
            "DELETE FROM fleet.pm_plan_assignments WHERE id = @id", seed.AssignmentId));
    }

    [Fact]
    public async Task Pm_read_models_are_tenant_isolated_and_system_writable()
    {
        var seed = await SeedPmRowsAsync(PostgresFixture.TenantA);

        // The rebuild runs on a system session — it writing all three rm_* rows at all is the
        // proof of the policies' system INSERT arm.
        await fixture.RebuildFleetProjectionsAsync();

        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.rm_maintenance_plans WHERE id = @id", seed.PlanId));
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.rm_pm_plan_assignments WHERE id = @id", seed.AssignmentId));
            Assert.Equal(1, await CountAsync(tenantA,
                "SELECT count(*) FROM fleet.rm_pm_completions WHERE id = @id", seed.CompletionId));
        }

        await using (var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB))
        {
            Assert.Equal(0, await CountAsync(tenantB,
                "SELECT count(*) FROM fleet.rm_maintenance_plans WHERE id = @id", seed.PlanId));
            Assert.Equal(0, await CountAsync(tenantB,
                "SELECT count(*) FROM fleet.rm_pm_plan_assignments WHERE id = @id", seed.AssignmentId));
            Assert.Equal(0, await CountAsync(tenantB,
                "SELECT count(*) FROM fleet.rm_pm_completions WHERE id = @id", seed.CompletionId));
        }

        // The system UPDATE arm, which the projection worker's upserts depend on.
        await using var system = await fixture.OpenRawConnectionAsync();
        await EnableSystemSessionAsync(system);
        Assert.Equal(1, await ExecuteAsync(system,
            "UPDATE fleet.rm_maintenance_plans SET version = version WHERE id = @id", seed.PlanId));
    }

    [Fact]
    public async Task System_session_reads_but_cannot_write_the_pm_write_tables()
    {
        var seed = await SeedPmRowsAsync(PostgresFixture.TenantA);

        await using var system = await fixture.OpenRawConnectionAsync();
        await EnableSystemSessionAsync(system);

        // The narrow system_read policies: the projection worker/rebuilder may SELECT
        // across tenants...
        Assert.Equal(1, await CountAsync(system,
            "SELECT count(*) FROM fleet.maintenance_plans WHERE id = @id", seed.PlanId));
        Assert.Equal(1, await CountAsync(system,
            "SELECT count(*) FROM fleet.pm_plan_assignments WHERE id = @id", seed.AssignmentId));
        Assert.Equal(1, await CountAsync(system,
            "SELECT count(*) FROM fleet.pm_completions WHERE id = @id", seed.CompletionId));

        // ...but never mutate the write side: UPDATE/DELETE match zero rows (no system arm
        // in any USING clause)...
        Assert.Equal(0, await ExecuteAsync(system,
            "UPDATE fleet.maintenance_plans SET notes = 'tampered' WHERE id = @id", seed.PlanId));
        Assert.Equal(0, await ExecuteAsync(system,
            "DELETE FROM fleet.pm_plan_assignments WHERE id = @id", seed.AssignmentId));
        Assert.Equal(0, await ExecuteAsync(system,
            "UPDATE fleet.pm_completions SET notes = 'tampered' WHERE id = @id", seed.CompletionId));

        // ...and an INSERT is rejected outright (no system arm in any WITH CHECK).
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(system,
            """
            INSERT INTO fleet.pm_plan_assignments (id, tenant_id, vehicle_id, plan_id, assigned_at_utc, version)
            VALUES (gen_random_uuid(), @id, gen_random_uuid(), gen_random_uuid(), now(), 1)
            """,
            PostgresFixture.TenantA));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task Pm_completions_are_append_only_even_for_the_owning_tenant()
    {
        var seed = await SeedPmRowsAsync(PostgresFixture.TenantA);

        await using var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA);

        // No UPDATE/DELETE policies exist for the ledger: with RLS forced, even the tenant
        // that wrote the completion matches zero rows (the event_journal precedent).
        Assert.Equal(0, await ExecuteAsync(tenantA,
            "UPDATE fleet.pm_completions SET odometer_km = 1 WHERE id = @id", seed.CompletionId));
        Assert.Equal(0, await ExecuteAsync(tenantA,
            "DELETE FROM fleet.pm_completions WHERE id = @id", seed.CompletionId));

        // And a tenant session cannot forge a row under another tenant's id — the INSERT
        // WITH CHECK pins tenant_id to the session tenant, with no system escape hatch.
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(tenantA,
            """
            INSERT INTO fleet.pm_completions
                (id, tenant_id, vehicle_id, plan_id, item_code, kind, performed_at, odometer_km,
                 performed_by, created_at_utc, version)
            VALUES
                (gen_random_uuid(), @id, gen_random_uuid(), gen_random_uuid(), 'PM-X-001', 'Item',
                 current_date, 1, 'forger', now(), 1)
            """,
            PostgresFixture.TenantB));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
