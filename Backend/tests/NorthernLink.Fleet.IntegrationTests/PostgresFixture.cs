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
using NorthernLink.Fleet.Infrastructure.Persistence.Projections;
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
            // northernlink_app is the ONLY role provisioned — exactly what
            // docker/initdb/01-app-role.sql now creates. The read side used to also need a
            // northernlink_projector role to own the matviews; this fixture creating it was
            // precisely why the tests passed while real databases crash-looped on 42704. Now the
            // read models are ordinary RLS-protected tables, so if any migration still depended
            // on that role, these tests would fail — which is the point.
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

    /// <summary>
    /// Rebuilds every Fleet read-model table from the write tables — the replacement for the old
    /// "REFRESH the matviews" helper. Used by tests that just need the read side populated,
    /// without exercising the journal/checkpoint path.
    /// </summary>
    public Task RebuildFleetProjectionsAsync() =>
        BuildFleetProjectionRebuilder().RebuildAsync();

    /// <summary>
    /// Builds a real <see cref="ProjectionRebuilder{FleetDbContext}"/> over this fixture's app
    /// connection, wired with the same registry the API composes.
    /// </summary>
    public ProjectionRebuilder<FleetDbContext> BuildFleetProjectionRebuilder()
    {
        var provider = BuildProjectionServices();

        return new ProjectionRebuilder<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildFleetRegistry(),
            provider.GetRequiredService<ILogger<ProjectionRebuilder<FleetDbContext>>>());
    }

    /// <summary>
    /// Builds a real <see cref="ProjectionWorker{FleetDbContext}"/> over this fixture's app
    /// connection (system session: no tenant), wired with the Fleet registry and the
    /// secondary-command handler, so tests can drive <c>ProcessOnceAsync</c> directly.
    /// </summary>
    public ProjectionWorker<FleetDbContext> BuildFleetProjectionWorker()
    {
        var provider = BuildProjectionServices();
        var options = new ProjectionOptions { PollInterval = TimeSpan.FromMilliseconds(1), BatchSize = 500 };

        return new ProjectionWorker<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildFleetRegistry(),
            options,
            provider.GetRequiredService<ILogger<ProjectionWorker<FleetDbContext>>>());
    }

    /// <summary>The same projection registry FleetServiceCollectionExtensions composes.</summary>
    private static IProjectionRegistry<FleetDbContext> BuildFleetRegistry() =>
        new ProjectionRegistryBuilder<FleetDbContext>(FleetServiceCollectionExtensions.SchemaName)
            .Project(new VehicleProjection())
            .Project(new RetirementCertificateProjection())
            .Project(new ShopProjection())
            .Project(new VehicleDocumentProjection())
            .Project(new ServiceRecordProjection())
            .Project(new WorkOrderProjection())
            .Project(new VehicleInspectionProjection())
            .OnEvent<VehicleReachedEndOfLifeDomainEvent>(entry =>
                new EnsureRetirementCertificateCommand(entry.AggregateId))
            .Build();

    private ServiceProvider BuildProjectionServices()
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

        return services.BuildServiceProvider();
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
