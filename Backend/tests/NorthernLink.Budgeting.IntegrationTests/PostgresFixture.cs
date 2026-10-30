using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NorthernLink.Budgeting.Application;
using NorthernLink.Budgeting.Infrastructure;
using NorthernLink.Budgeting.Infrastructure.Persistence;
using NorthernLink.Budgeting.Infrastructure.Persistence.Projections;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Budgeting.IntegrationTests;

/// <summary>
/// One Postgres container for the whole collection, provisioned like the live server: the schema
/// is migrated and owned by the NON-superuser role northernlink_app, because a superuser bypasses
/// Row-Level Security even with FORCE (the Fleet/Drivers fixture arrangement).
/// <para>
/// <b>The identity.users stub.</b> Two Budgeting migrations (BackfillBudgetingUserLookup,
/// AddUserLookupFullName) read identity.users — on a real database the Identity module's table.
/// This fixture migrates Budgeting alone, so it creates an empty table with just the columns those
/// two statements read. Empty is the honest state: those backfills have nothing to do here.
/// </para>
/// <para>
/// Besides the main database, <see cref="CreateBlankDatabaseAsync"/> hands out fresh, unmigrated
/// databases in the same container — for migration tests that walk a database Up, Down and Up again
/// and must not disturb the shared one.
/// </para>
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
            await ExecAsync(admin, $"CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}';");
        }

        AppConnectionString = await ProvisionDatabaseAsync("northernlink", create: false);

        await using var context = CreateContext(AppConnectionString, tenantId: null);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// A fresh database in this container, provisioned exactly like the main one (app role grants,
    /// identity.users stub) but NOT migrated. Returns the app-role connection string.
    /// </summary>
    public Task<string> CreateBlankDatabaseAsync() =>
        ProvisionDatabaseAsync($"blank_{Guid.NewGuid():N}", create: true);

    private async Task<string> ProvisionDatabaseAsync(string database, bool create)
    {
        if (create)
        {
            await using var admin = new NpgsqlConnection(_container.GetConnectionString());
            await admin.OpenAsync();
            await ExecAsync(admin, $"CREATE DATABASE {database};");
        }

        var adminInDatabase = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database };
        await using (var admin = new NpgsqlConnection(adminInDatabase.ConnectionString))
        {
            await admin.OpenAsync();
            await ExecAsync(
                admin,
                $"""
                GRANT CONNECT ON DATABASE {database} TO {AppRole};
                GRANT CREATE ON DATABASE {database} TO {AppRole};
                CREATE SCHEMA identity;
                CREATE TABLE identity.users (
                    id uuid PRIMARY KEY, tenant_id uuid NOT NULL, email text NOT NULL,
                    role text NOT NULL, full_name text NULL);
                GRANT USAGE ON SCHEMA identity TO {AppRole};
                GRANT SELECT ON identity.users TO {AppRole};
                """);
        }

        return new NpgsqlConnectionStringBuilder(adminInDatabase.ConnectionString)
        {
            Username = AppRole,
            Password = AppPassword,
        }.ConnectionString;
    }

    /// <summary>A real BudgetingDbContext on the main database, exactly as the API composes it.</summary>
    public BudgetingDbContext CreateContext(Guid? tenantId) => CreateContext(AppConnectionString, tenantId);

    /// <summary>A real BudgetingDbContext on the given database, for the given tenant.</summary>
    public static BudgetingDbContext CreateContext(string connectionString, Guid? tenantId)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<BudgetingDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BudgetingServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new BudgetingDbContext(options, tenantContext, new BudgetingIntegrationEventMapper());
    }

    /// <summary>Raw connection as the app role, optionally with a tenant RLS session variable.</summary>
    public static async Task<NpgsqlConnection> OpenRawConnectionAsync(string connectionString, Guid? tenantId = null)
    {
        var connection = new NpgsqlConnection(connectionString);
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
    /// A real projection worker over the given database (system session: no tenant), wired with the
    /// same registry <c>BudgetingServiceCollectionExtensions.AddBudgeting</c> composes. One
    /// <c>ProcessOnceAsync</c> runs the per-aggregate <c>ApplyAsync</c> path — the one where the
    /// write-side source is TRACKED in the same context as the read row it maps onto.
    /// </summary>
    public static ProjectionWorker<BudgetingDbContext> BuildProjectionWorker(string connectionString)
    {
        var provider = BuildProjectionServices(connectionString);
        var options = new ProjectionOptions { PollInterval = TimeSpan.FromMilliseconds(1), BatchSize = 500 };

        return new ProjectionWorker<BudgetingDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildRegistry(),
            options,
            provider.GetRequiredService<ILogger<ProjectionWorker<BudgetingDbContext>>>());
    }

    /// <summary>Rebuilds every Budgeting read table from the write tables.</summary>
    public static Task RebuildProjectionsAsync(string connectionString)
    {
        var provider = BuildProjectionServices(connectionString);
        return new ProjectionRebuilder<BudgetingDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            BuildRegistry(),
            provider.GetRequiredService<ILogger<ProjectionRebuilder<BudgetingDbContext>>>()).RebuildAsync();
    }

    /// <summary>The same projection registry BudgetingServiceCollectionExtensions composes.</summary>
    private static IProjectionRegistry<BudgetingDbContext> BuildRegistry() =>
        new ProjectionRegistryBuilder<BudgetingDbContext>(BudgetingServiceCollectionExtensions.SchemaName)
            .Project(new BudgetPeriodProjection())
            .Project(new BudgetCodeProjection())
            .Project(new BudgetAllocationProjection())
            .Project(new VendorProjection())
            .Project(new CostCentreProjection())
            .Build();

    private static ServiceProvider BuildProjectionServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(null));
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<BudgetingDbContext>((sp, options) => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BudgetingServiceCollectionExtensions.SchemaName))
            .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

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
