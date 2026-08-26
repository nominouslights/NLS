using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections.PropagateOdometer;
using NorthernLink.Fleet.Application.Maintenance.Completions.PropagateOdometer;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Application.Vehicles.EnsureRetirementCertificate;
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

    // One root provider for every projection worker/rebuilder the fixture hands out —
    // built lazily (the connection string exists only after InitializeAsync) and disposed
    // with the fixture, so no per-call ServiceProvider is ever leaked.
    private ServiceProvider? _projectionServices;

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

    public async Task DisposeAsync()
    {
        if (_projectionServices is not null)
        {
            await _projectionServices.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

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
        var provider = ProjectionServices;

        return new ProjectionRebuilder<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildFleetRegistry(),
            provider.GetRequiredService<ILogger<ProjectionRebuilder<FleetDbContext>>>());
    }

    /// <summary>
    /// Loops <c>ProcessOnceAsync</c> until the fleet journal is quiescent. A single poll is
    /// never guaranteed to drain it: one poll takes at most <c>BatchSize</c> rows, and the
    /// secondary commands a poll dispatches run AFTER that poll's checkpoint write — a
    /// reaction (odometer propagation, retirement certificate) that mutates an aggregate
    /// appends NEW journal rows the finished poll's checkpoint does not cover. Quiescent =
    /// a poll during which the journal head did not move and after which the checkpoint has
    /// caught that head. Use this wherever a test assumes "the backlog is cleared".
    /// </summary>
    public async Task DrainFleetProjectionsAsync()
    {
        var worker = BuildFleetProjectionWorker();

        // Reaction cascades are short (a reaction's own append triggers no further reaction
        // in the current registry), so a bounded loop separates "still catching up" from a
        // genuine livelock — which should fail loudly, not spin forever.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var headBefore = await ReadFleetJournalHeadAsync();
            await worker.ProcessOnceAsync(CancellationToken.None);

            if (await ReadFleetJournalHeadAsync() == headBefore
                && await ReadFleetProjectionCheckpointAsync() >= headBefore)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "fleet.event_journal did not quiesce after 50 polls — a reaction appears to append new rows on every poll.");
    }

    /// <summary>The fleet projection worker's checkpoint cursor (0 before its first poll).</summary>
    public Task<long> ReadFleetProjectionCheckpointAsync() => ScalarUnderSystemAsync(
        $"SELECT coalesce((SELECT last_position FROM fleet.projection_checkpoints WHERE projection_name = '{FleetServiceCollectionExtensions.SchemaName}'), 0);");

    /// <summary>Max position in fleet.event_journal (0 when empty), read under the system RLS policy.</summary>
    public Task<long> ReadFleetJournalHeadAsync() => ScalarUnderSystemAsync(
        "SELECT coalesce(max(position), 0) FROM fleet.event_journal;");

    private async Task<long> ScalarUnderSystemAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();
        await using (var system = new NpgsqlCommand(
            "SELECT set_config('app.is_system', 'true', false);", connection))
        {
            await system.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Builds a real <see cref="ProjectionWorker{FleetDbContext}"/> over this fixture's app
    /// connection (system session: no tenant), wired with the Fleet registry and the
    /// secondary-command handler, so tests can drive <c>ProcessOnceAsync</c> directly.
    /// </summary>
    public ProjectionWorker<FleetDbContext> BuildFleetProjectionWorker()
    {
        var provider = ProjectionServices;
        var options = new ProjectionOptions { PollInterval = TimeSpan.FromMilliseconds(1), BatchSize = 500 };

        return new ProjectionWorker<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildFleetRegistry(),
            options,
            provider.GetRequiredService<ILogger<ProjectionWorker<FleetDbContext>>>());
    }

    /// <summary>
    /// The production registry, verbatim: composed by the same
    /// <see cref="FleetProjectionRegistry.Configure"/> call that
    /// <c>FleetServiceCollectionExtensions.AddFleet</c> passes to <c>AddProjections</c>, so
    /// the fixture can never drift to a narrower read side — or fewer OnEvent reactions —
    /// than the API runs. Every reaction's handler is registered in the service collection
    /// below.
    /// </summary>
    private static IProjectionRegistry<FleetDbContext> BuildFleetRegistry()
    {
        var builder = new ProjectionRegistryBuilder<FleetDbContext>(FleetServiceCollectionExtensions.SchemaName);
        FleetProjectionRegistry.Configure(builder);
        return builder.Build();
    }

    private ServiceProvider ProjectionServices => _projectionServices ??= BuildProjectionServices();

    private ServiceProvider BuildProjectionServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Ambient-aware, mirroring JwtTenantContext's background-work fallback: null during
        // the poll/rebuild (the system session), the journal row's tenant inside the
        // worker's AmbientTenant.Push when a secondary command handler runs — a fixed-null
        // context here would make every OnEvent handler see zero rows through the query
        // filters and RLS, silently no-opping the reactions these tests exist to exercise.
        services.AddScoped<ITenantContext, AmbientTenantContext>();
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<FleetDbContext>((sp, options) => options
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleInspectionRepository, VehicleInspectionRepository>();
        services.AddScoped<IPmCompletionRepository, PmCompletionRepository>();
        services.AddScoped<ICommandHandler<EnsureRetirementCertificateCommand>, EnsureRetirementCertificateCommandHandler>();
        services.AddScoped<ICommandHandler<PropagateInspectionOdometerCommand>, PropagateInspectionOdometerCommandHandler>();
        services.AddScoped<ICommandHandler<PropagatePmOdometerCommand>, PropagatePmOdometerCommandHandler>();

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

    /// <summary>
    /// The projection-services tenant context: no principal in tests, so the tenant is
    /// whatever <see cref="AmbientTenant"/> the projection worker pushed for the current
    /// command scope (null during the poll itself) — exactly JwtTenantContext's fallback arm.
    /// </summary>
    private sealed class AmbientTenantContext : ITenantContext
    {
        public Guid? TenantId => AmbientTenant.Current;

        public TenantType? TenantType => TenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
