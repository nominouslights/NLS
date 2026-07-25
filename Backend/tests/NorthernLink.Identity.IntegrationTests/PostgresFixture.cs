using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Identity.Infrastructure;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Identity.IntegrationTests;

/// <summary>
/// One Postgres container for the whole collection, provisioned like the live server:
/// the schema is migrated and owned by the NON-superuser role northernlink_app
/// (mirroring docker/initdb/01-app-role.sql), because a superuser bypasses Row-Level
/// Security even with FORCE — connecting as one would make the app.is_system escape
/// hatch (and every RLS-adjacent assertion here) meaningless.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppRole = "northernlink_app";
    private const string AppPassword = "northernlink_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("northernlink")
        .Build();

    public string AppConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var provision = new NpgsqlCommand(
                $"""
                CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}';
                GRANT CONNECT ON DATABASE northernlink TO {AppRole};
                GRANT CREATE ON DATABASE northernlink TO {AppRole};
                """, admin);
            await provision.ExecuteNonQueryAsync();
        }

        AppConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = AppRole,
            Password = AppPassword,
        }.ConnectionString;

        // Migrations run as the app role, which then owns the identity schema; FORCE RLS in
        // the migrations binds the owner too — same arrangement as the live server.
        await using var context = CreateContext(tenantId: null);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A real IdentityDbContext exactly as the API composes it, for the given tenant.</summary>
    public IdentityDbContext CreateContext(Guid? tenantId)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new IdentityDbContext(options, tenantContext);
    }

    /// <summary>
    /// Raw connection as the app role, opted into the app.is_system RLS escape hatch —
    /// the same session shape Identity's anonymous flows use — for direct SQL assertions.
    /// </summary>
    public async Task<NpgsqlConnection> OpenSystemConnectionAsync()
    {
        var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.is_system', 'true', false);", connection);
        await command.ExecuteNonQueryAsync();

        return connection;
    }

    /// <summary>
    /// Empties identity.users so a test can reopen the one-time first-run gate. Tests in
    /// this collection run sequentially, so this cannot race another test.
    /// </summary>
    public async Task ResetUsersAsync()
    {
        await using var connection = await OpenSystemConnectionAsync();
        await using var command = new NpgsqlCommand("DELETE FROM identity.users;", connection);
        await command.ExecuteNonQueryAsync();
    }

    public sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;

        public TenantType? TenantType => tenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
