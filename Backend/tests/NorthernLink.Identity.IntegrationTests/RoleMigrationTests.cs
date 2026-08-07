using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NorthernLink.Identity.Infrastructure;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Identity.IntegrationTests;

/// <summary>
/// Proves RenameAdminRoleToOwner actually rewrites rows on a live, RLS-enforcing database.
///
/// This is the trap the test exists for: identity.users has FORCE ROW LEVEL SECURITY, a
/// migration connection sets neither app.tenant_id nor app.is_system, and FORCE binds the table
/// owner too — so a migration whose UPDATE lacks the app.is_system guard matches zero rows and
/// still reports complete success. Nothing short of running the migration against a real
/// non-superuser session catches that: unit tests never touch a policy, and a superuser
/// connection bypasses RLS entirely and would pass either way.
///
/// Owns its own container rather than joining the "postgres" collection, because PostgresFixture
/// migrates straight to latest in InitializeAsync and this test has to observe the state before
/// and after one specific migration.
/// </summary>
public sealed class RoleMigrationTests : IAsyncLifetime
{
    private const string AppRole = "northernlink_app";
    private const string AppPassword = "northernlink_test";

    /// <summary>The migration immediately preceding RenameAdminRoleToOwner.</summary>
    private const string BeforeRename = "20260804192152_AddBootstrapTokenRole";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("northernlink")
        .Build();

    private string _appConnectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var admin = new NpgsqlConnection(_container.GetConnectionString());
        await admin.OpenAsync();
        await using var provision = new NpgsqlCommand(
            $"""
            CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}';
            GRANT CONNECT ON DATABASE northernlink TO {AppRole};
            GRANT CREATE ON DATABASE northernlink TO {AppRole};
            """, admin);
        await provision.ExecuteNonQueryAsync();

        _appConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = AppRole,
            Password = AppPassword,
        }.ConnectionString;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private IdentityDbContext CreateContext()
    {
        var tenantContext = new PostgresFixture.TestTenantContext(null);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                _appConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory", IdentityServiceCollectionExtensions.SchemaName))
            .Options;

        return new IdentityDbContext(options, tenantContext);
    }

    private async Task MigrateToAsync(string? targetMigration)
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    /// <summary>
    /// Inserts straight through SQL on a system session. User.Create would reject "Admin" now —
    /// which is the point: this row has to look like one written before the role model existed.
    /// </summary>
    private async Task InsertUserAsync(Guid id, string email, string role)
    {
        await using var connection = new NpgsqlConnection(_appConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config('app.is_system', 'true', false);
            INSERT INTO identity.users
                (id, tenant_id, email, password_hash, role, created_at_utc, version)
            VALUES (@id, @tenantId, @email, 'hashed:pw', @role, now(), 1);
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenantId", SeedTenant.Id);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("role", role);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> ReadRoleAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(_appConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config('app.is_system', 'true', false);
            SELECT role FROM identity.users WHERE id = @id;
            """, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.NextResultAsync(); // step past set_config's result set
        Assert.True(await reader.ReadAsync(), $"No identity.users row for {id}.");
        return reader.GetString(0);
    }

    [Fact]
    public async Task Legacy_Admin_users_become_Owner_despite_FORCE_row_level_security()
    {
        await MigrateToAsync(BeforeRename);

        var legacyAdmin = Guid.NewGuid();
        var alreadyDispatcher = Guid.NewGuid();
        await InsertUserAsync(legacyAdmin, "legacy-admin@northernlink.ca", Roles.LegacyAdmin);
        await InsertUserAsync(alreadyDispatcher, "dispatcher@northernlink.ca", Roles.Dispatcher);

        Assert.Equal(Roles.LegacyAdmin, await ReadRoleAsync(legacyAdmin));

        await MigrateToAsync(targetMigration: null); // null = latest

        // The assertion that matters: without set_config('app.is_system', …) inside the
        // migration, this still reads "Admin" and the migration reported success anyway.
        Assert.Equal(Roles.Owner, await ReadRoleAsync(legacyAdmin));

        // And it is a targeted rewrite, not a blanket one.
        Assert.Equal(Roles.Dispatcher, await ReadRoleAsync(alreadyDispatcher));
    }

    [Fact]
    public async Task Bootstrap_tokens_predating_the_role_column_are_backfilled_to_Owner()
    {
        // AddBootstrapTokenRole backfills through the same RLS-guarded UPDATE, and its ALTER to
        // NOT NULL would fail outright if the backfill silently matched nothing — so this covers
        // both the backfill and the guard.
        await MigrateToAsync(targetMigration: null);

        await using var connection = new NpgsqlConnection(_appConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT is_nullable FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name = 'admin_bootstrap_tokens'
              AND column_name = 'role';
            """, connection);

        Assert.Equal("NO", await command.ExecuteScalarAsync() as string);
    }
}
