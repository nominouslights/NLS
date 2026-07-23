using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Infrastructure;
using NorthernLink.Drivers.Infrastructure.Persistence;
using NorthernLink.Drivers.Infrastructure.Persistence.Projections;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// One Postgres container for the whole collection, provisioned like the live server:
/// the schema is migrated and owned by the NON-superuser role northernlink_app
/// (mirroring docker/initdb/01-app-role.sql), because a superuser bypasses Row-Level
/// Security even with FORCE — connecting as one would make every RLS-dependent
/// assertion here meaningless. Same arrangement as the Fleet integration fixture.
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

        // Migrations run as the app role, which then owns the drivers schema; FORCE RLS in
        // the migrations binds the owner too — same arrangement as the live server.
        await using var context = CreateContext(tenantId: null);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A real DriversDbContext exactly as the API composes it, for the given tenant.</summary>
    public DriversDbContext CreateContext(Guid? tenantId, bool withMapper = false)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<DriversDbContext>()
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DriversServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new DriversDbContext(options, tenantContext, withMapper ? new DriversIntegrationEventMapper() : null);
    }

    /// <summary>
    /// Builds a real <see cref="ProjectionWorker{DriversDbContext}"/> over this fixture's app
    /// connection (system session: no tenant), wired with the same registry
    /// <c>DriversServiceCollectionExtensions.AddDrivers</c> composes, so tests can drive
    /// <c>ProcessOnceAsync</c> directly.
    /// </summary>
    public ProjectionWorker<DriversDbContext> BuildDriversProjectionWorker()
    {
        var provider = BuildProjectionServices();
        var options = new ProjectionOptions { PollInterval = TimeSpan.FromMilliseconds(1), BatchSize = 500 };

        return new ProjectionWorker<DriversDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildDriversRegistry(),
            options,
            provider.GetRequiredService<ILogger<ProjectionWorker<DriversDbContext>>>());
    }

    /// <summary>The same projection registry DriversServiceCollectionExtensions composes.</summary>
    private static IProjectionRegistry<DriversDbContext> BuildDriversRegistry() =>
        new ProjectionRegistryBuilder<DriversDbContext>(DriversServiceCollectionExtensions.SchemaName)
            .Project(new DriverProjection())
            .Project(new DriverCredentialProjection())
            .Project(new DriverClearanceProjection())
            .Project(new HosLogEntryProjection())
            .Build();

    private ServiceProvider BuildProjectionServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(null));
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<DriversDbContext>((sp, options) => options
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DriversServiceCollectionExtensions.SchemaName))
            .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services.BuildServiceProvider();
    }

    public sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;

        public TenantType? TenantType => tenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
