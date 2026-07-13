using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// One Postgres container for the whole collection, provisioned like the live server:
/// the schema is migrated and owned by the NON-superuser role northernlink_app
/// (mirroring docker/initdb/01-app-role.sql), because a superuser bypasses Row-Level
/// Security even with FORCE — connecting as one would make every RLS assertion here
/// meaningless.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppRole = "northernlink_app";
    private const string AppPassword = "northernlink_test";

    public static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

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

        // Migrations run as the app role, which then owns the fleet schema; FORCE RLS in
        // the migrations binds the owner too — same arrangement as the live server.
        await using var context = CreateContext(tenantId: null);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A real FleetDbContext exactly as the API composes it, for the given tenant.</summary>
    public FleetDbContext CreateContext(Guid? tenantId, bool withMapper = false)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new FleetDbContext(options, tenantContext, withMapper ? new FleetIntegrationEventMapper() : null);
    }

    /// <summary>Raw connection as the app role, optionally with a tenant RLS session variable.</summary>
    public async Task<NpgsqlConnection> OpenRawConnectionAsync(Guid? tenantId = null)
    {
        var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();

        if (tenantId is { } tenant)
        {
            await using var command = new NpgsqlCommand(
                "SELECT set_config('app.tenant_id', @tenant_id, false);", connection);
            command.Parameters.AddWithValue("tenant_id", tenant.ToString());
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }

    public sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;

        public TenantType? TenantType => tenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
