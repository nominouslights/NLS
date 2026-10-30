using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Budgeting.IntegrationTests;

/// <summary>
/// The actuals tables on real Postgres, as the non-superuser app role: what the in-memory fakes
/// cannot see. Two things are pinned here.
/// <list type="bullet">
/// <item><b>Row-Level Security</b> on <c>budgeting.actual_transactions</c> and
/// <c>budgeting.rm_actual_transactions</c>: another tenant's session sees nothing, a tenant-less
/// session (a pooled connection whose <c>app.tenant_id</c> was reset to the empty string) sees
/// nothing and does not throw 22P02 — the <c>NULLIF</c> in every policy — and the
/// <c>app.is_system</c> path the projection worker and outbox dispatcher use reads across tenants,
/// while on the write table it stays read-only (its system policy is <c>FOR SELECT</c>).</item>
/// <item><b>The SQL translation</b> of <see cref="ActualTransactionRepository.ExistsForCodeAsync"/>,
/// which the retire-code and delete-code paths ask in the same request: both date bounds are
/// inclusive, the string arm matches a transaction on another code id, and a transaction one day
/// outside the range does not count.</item>
/// </list>
/// Raw SQL for the RLS checks on purpose: the DbContext's tenant query filter would hide another
/// tenant's rows by itself, and the point is that the database refuses them too.
/// Each test uses fresh tenants so rows never collide across tests in the shared collection.
/// </summary>
[Collection("postgres")]
public class ActualTransactionsPersistenceTests(PostgresFixture fixture)
{
    private static ActualTransaction Actual(Guid tenant, Guid codeId, string code, DateOnly occurredOn) =>
        ActualTransaction.Create(
            tenant, codeId, code, 125.00m, occurredOn, "Diesel, Yellowknife depot.",
            ActualTransactionSource.Manual, externalReference: null, actorId: null).Value;

    private async Task<ActualTransaction> SeedAsync(Guid tenant, Guid codeId, string code, DateOnly occurredOn)
    {
        var transaction = Actual(tenant, codeId, code, occurredOn);
        await using var context = fixture.CreateContext(tenant);
        context.ActualTransactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }

    /// <summary>Two tenants with one transaction each, projected into the read table.</summary>
    private async Task<(Guid TenantA, Guid TenantB)> SeedTwoTenantsAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedAsync(tenantA, Guid.NewGuid(), "ZBB-FUEL-01", new DateOnly(2026, 10, 5));
        await SeedAsync(tenantB, Guid.NewGuid(), "ZBB-FUEL-01", new DateOnly(2026, 10, 5));

        await PostgresFixture.BuildProjectionWorker(fixture.AppConnectionString).ProcessOnceAsync(CancellationToken.None);
        return (tenantA, tenantB);
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table, Guid tenant)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT count(*) FROM budgeting.{table} WHERE tenant_id = @tenant;", connection);
        command.Parameters.AddWithValue("tenant", tenant);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetAsync(NpgsqlConnection connection, string setting, string value)
    {
        await using var command = new NpgsqlCommand("SELECT set_config(@setting, @value, false);", connection);
        command.Parameters.AddWithValue("setting", setting);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync();
    }

    // --- Row-Level Security ---

    [Theory]
    [InlineData("actual_transactions")]
    [InlineData("rm_actual_transactions")]
    public async Task A_tenant_session_sees_its_own_rows_and_none_of_another_tenants(string table)
    {
        var (tenantA, tenantB) = await SeedTwoTenantsAsync();

        await using var connection = await PostgresFixture.OpenRawConnectionAsync(fixture.AppConnectionString, tenantA);

        Assert.Equal(1, await CountAsync(connection, table, tenantA));
        Assert.Equal(0, await CountAsync(connection, table, tenantB));
    }

    [Theory]
    [InlineData("actual_transactions")]
    [InlineData("rm_actual_transactions")]
    public async Task A_tenant_less_pooled_session_sees_nothing_and_does_not_throw(string table)
    {
        // Once a session has set app.tenant_id, a pool reset leaves it as '' rather than unset;
        // without NULLIF the policy casts '' to uuid and every query throws 22P02.
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using var connection = await PostgresFixture.OpenRawConnectionAsync(fixture.AppConnectionString, tenantA);
        await SetAsync(connection, "app.tenant_id", "");

        Assert.Equal(0, await CountAsync(connection, table, tenantA));
    }

    [Theory]
    [InlineData("actual_transactions")]
    [InlineData("rm_actual_transactions")]
    public async Task A_session_with_no_tenant_setting_at_all_sees_nothing(string table)
    {
        var (tenantA, _) = await SeedTwoTenantsAsync();

        await using var connection = await PostgresFixture.OpenRawConnectionAsync(fixture.AppConnectionString);

        Assert.Equal(0, await CountAsync(connection, table, tenantA));
    }

    [Theory]
    [InlineData("actual_transactions")]
    [InlineData("rm_actual_transactions")]
    public async Task The_system_session_reads_across_tenants(string table)
    {
        // The path the projection worker and outbox dispatcher take: no tenant, app.is_system.
        // The projection having filled rm_actual_transactions at all already depends on it.
        var (tenantA, tenantB) = await SeedTwoTenantsAsync();

        await using var connection = await PostgresFixture.OpenRawConnectionAsync(fixture.AppConnectionString);
        await SetAsync(connection, "app.is_system", "true");

        Assert.Equal(1, await CountAsync(connection, table, tenantA));
        Assert.Equal(1, await CountAsync(connection, table, tenantB));
    }

    [Fact]
    public async Task The_system_session_cannot_write_the_write_table()
    {
        // actual_transactions' system policy is FOR SELECT only: app.is_system reads, it never
        // inserts on a tenant's behalf. 42501 is the RLS WITH CHECK refusal.
        await using var connection = await PostgresFixture.OpenRawConnectionAsync(fixture.AppConnectionString);
        await SetAsync(connection, "app.is_system", "true");

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO budgeting.actual_transactions
                (id, tenant_id, budget_code_id, code, amount_cad, occurred_on, description, source,
                 created_at_utc, updated_at_utc)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'ZBB-FUEL-01', 1,
                    DATE '2026-10-05', 'x', 'Manual', now(), now());
            """,
            connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    // --- ExistsForCodeAsync translation ---

    private async Task<bool> ExistsAsync(Guid tenant, Guid codeId, string code, DateOnly startsOn, DateOnly endsOn)
    {
        await using var context = fixture.CreateContext(tenant);
        return await new ActualTransactionRepository(context).ExistsForCodeAsync(codeId, code, startsOn, endsOn);
    }

    private static readonly DateOnly Q4Start = new(2026, 10, 1);
    private static readonly DateOnly Q4End = new(2026, 12, 31);

    [Fact]
    public async Task Both_bounds_are_inclusive()
    {
        var tenant = Guid.NewGuid();
        var first = Guid.NewGuid();
        var last = Guid.NewGuid();
        await SeedAsync(tenant, first, "ZBB-FIRST-01", Q4Start);
        await SeedAsync(tenant, last, "ZBB-LAST-01", Q4End);

        Assert.True(await ExistsAsync(tenant, first, "ZBB-FIRST-01", Q4Start, Q4End));
        Assert.True(await ExistsAsync(tenant, last, "ZBB-LAST-01", Q4Start, Q4End));
    }

    [Fact]
    public async Task One_day_outside_either_bound_does_not_count()
    {
        var tenant = Guid.NewGuid();
        var codeId = Guid.NewGuid();
        await SeedAsync(tenant, codeId, "ZBB-FUEL-01", Q4Start.AddDays(-1));
        await SeedAsync(tenant, codeId, "ZBB-FUEL-01", Q4End.AddDays(1));

        Assert.False(await ExistsAsync(tenant, codeId, "ZBB-FUEL-01", Q4Start, Q4End));
        // Same rows, a range that does reach them: the false above is the bounds, not the match.
        Assert.True(await ExistsAsync(tenant, codeId, "ZBB-FUEL-01", Q4Start.AddDays(-1), Q4End));
    }

    [Fact]
    public async Task The_string_arm_matches_a_transaction_on_another_code_id()
    {
        // A recreated code: the old transaction carries the string but a different id.
        var tenant = Guid.NewGuid();
        await SeedAsync(tenant, Guid.NewGuid(), "ZBB-FUEL-01", new DateOnly(2026, 11, 15));

        Assert.True(await ExistsAsync(tenant, Guid.NewGuid(), "ZBB-FUEL-01", Q4Start, Q4End));
        Assert.False(await ExistsAsync(tenant, Guid.NewGuid(), "ZBB-CREW-01", Q4Start, Q4End));
    }

    [Fact]
    public async Task Another_tenants_transaction_never_counts()
    {
        var owner = Guid.NewGuid();
        var codeId = Guid.NewGuid();
        await SeedAsync(owner, codeId, "ZBB-FUEL-01", new DateOnly(2026, 11, 15));

        Assert.False(await ExistsAsync(Guid.NewGuid(), codeId, "ZBB-FUEL-01", Q4Start, Q4End));
    }
}
