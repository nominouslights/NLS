using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Infrastructure;
using NorthernLink.Trips.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// One Postgres container for the whole collection, with BOTH the fleet and trips schemas
/// migrated — the cross-module surface the outbox polling consumer spans. Provisioned like
/// the live server: schemas migrated and owned by the non-superuser role northernlink_app,
/// because a superuser bypasses Row-Level Security even with FORCE (mirrors the Fleet
/// integration-test fixture).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppRole = "northernlink_app";
    private const string AppPassword = "northernlink_test";

    public static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-00000000000a");

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

        await using (var fleet = CreateFleetContext(tenantId: null))
        {
            await fleet.Database.MigrateAsync();
        }

        await using (var trips = CreateTripsContext(tenantId: null))
        {
            await trips.Database.MigrateAsync();
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A real TripsDbContext exactly as the API composes it, for the given tenant.</summary>
    public TripsDbContext CreateTripsContext(Guid? tenantId)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<TripsDbContext>()
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", TripsServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new TripsDbContext(options, tenantContext);
    }

    /// <summary>A real FleetDbContext (the producer side), for the given tenant.</summary>
    public FleetDbContext CreateFleetContext(Guid? tenantId)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new FleetDbContext(options, tenantContext, null);
    }

    public sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;

        public TenantType? TenantType => tenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }

    /// <summary>
    /// The tenant context handler scopes get in production background work: the ambient
    /// tenant pushed by the handler itself (from the event payload), so the
    /// TenantSessionInterceptor sets app.tenant_id exactly as for a real request.
    /// </summary>
    public sealed class AmbientTenantContext : ITenantContext
    {
        public Guid? TenantId => AmbientTenant.Current;

        public TenantType? TenantType => TenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
