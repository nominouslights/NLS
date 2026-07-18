using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Application.Vehicles.EnsureRetirementCertificate;
using NorthernLink.Fleet.Domain.Vehicles.Events;
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

                -- Read-side projection owner, mirroring docker/initdb/01-app-role.sql: the
                -- matviews and their tenant wrapper views are owned by projector; the app is
                -- a granted member so it can ALTER … OWNER TO projector (migrations) and
                -- SET ROLE projector (worker REFRESH). Provisioned here so the RLS/refresh
                -- backstop runs under the same non-superuser arrangement as the live server.
                CREATE ROLE northernlink_projector NOLOGIN;
                GRANT northernlink_projector TO {AppRole} WITH INHERIT FALSE;
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

    /// <summary>
    /// Refreshes Fleet matviews exactly as the projection worker does — a pinned system
    /// session (<c>app.is_system</c>) under <c>SET ROLE northernlink_projector</c>. Used by the
    /// parity and RLS tests, which only need the matviews populated, not the checkpoint logic.
    /// Non-concurrent refresh (always valid regardless of populated state).
    /// </summary>
    public async Task RefreshFleetMatviewsAsync(params string[] matviews)
    {
        await using var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();
        await ExecAsync(connection, "SELECT set_config('app.is_system', 'true', false);");
        await ExecAsync(connection, "SET ROLE northernlink_projector;");
        foreach (var matview in matviews)
        {
            await ExecAsync(connection, $"REFRESH MATERIALIZED VIEW fleet.{matview};");
        }

        await ExecAsync(connection, "RESET ROLE;");
    }

    /// <summary>
    /// Builds a real <see cref="ProjectionWorker{FleetDbContext}"/> over this fixture's app
    /// connection (system session: no tenant), wired with the Fleet registry and the
    /// secondary-command handler, so tests can drive <c>ProcessOnceAsync</c> directly.
    /// </summary>
    public ProjectionWorker<FleetDbContext> BuildFleetProjectionWorker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(null));
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<FleetDbContext>((sp, options) => options
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<ICommandHandler<EnsureRetirementCertificateCommand>, EnsureRetirementCertificateCommandHandler>();

        var provider = services.BuildServiceProvider();

        var registry = new ProjectionRegistryBuilder(FleetServiceCollectionExtensions.SchemaName)
            .OnAggregate("vehicle", "mv_vehicles", "mv_retirement_certificates")
            .OnAggregate("shop", "mv_shops")
            .OnAggregate("vehicle-document", "mv_vehicle_documents")
            .OnAggregate("service-record", "mv_service_records")
            .OnAggregate("work-order", "mv_work_orders")
            .OnAggregate("vehicle-inspection", "mv_vehicle_inspections")
            .OnEvent<VehicleReachedEndOfLifeDomainEvent>(entry =>
                new EnsureRetirementCertificateCommand(entry.AggregateId))
            .Build();

        var options = new ProjectionOptions { PollInterval = TimeSpan.FromMilliseconds(1), BatchSize = 500 };

        return new ProjectionWorker<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            options,
            provider.GetRequiredService<ILogger<ProjectionWorker<FleetDbContext>>>());
    }

    private static async Task ExecAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
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
