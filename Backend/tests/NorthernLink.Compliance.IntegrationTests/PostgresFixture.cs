using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NorthernLink.Compliance.Infrastructure;
using NorthernLink.Compliance.Infrastructure.Persistence;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using Testcontainers.PostgreSql;
using Xunit;

namespace NorthernLink.Compliance.IntegrationTests;

/// <summary>
/// One Postgres container for the collection, provisioned like the live server: the compliance
/// schema migrated by, and owned by, the NON-superuser role <c>northernlink_app</c> — a superuser
/// bypasses Row-Level Security even with FORCE, so only this shape proves the policies hold.
/// <para>
/// Stand-in <c>outbox_messages</c> tables are created for every producer schema the module polls with the exact shape of every module's outbox
/// (<c>LIKE compliance.outbox_messages</c>) so the fan-out path can be driven from a "producer"
/// schema without referencing the Fleet library — a domain library's tests stay as isolated as the
/// library. Like the real producer outboxes it carries the system-dispatch policy the polling
/// consumer reads under.
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

        // AddCompliance reads the connection string from the environment inside its factory
        // lambda, exactly as in production; this assembly is the only thing in its process.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", AppConnectionString);

        await using (var compliance = CreateContext(tenantId: null))
        {
            await compliance.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();
        foreach (var schema in ProducerSchemas)
        {
            await using var producer = new NpgsqlCommand(
                $"""
                CREATE SCHEMA {schema};
                CREATE TABLE {schema}.outbox_messages (LIKE compliance.outbox_messages INCLUDING ALL);
                ALTER TABLE {schema}.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {schema}.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON {schema}.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON {schema}.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');
                """, connection);
            await producer.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Every other schema the Compliance consumer polls (record producers + replica sources).</summary>
    private static readonly string[] ProducerSchemas = ["drivers", "fleet", "identity", "trips"];

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A real ComplianceDbContext for <paramref name="tenantId"/> (null = no tenant session).</summary>
    public ComplianceDbContext CreateContext(Guid? tenantId)
    {
        var tenantContext = new FixedTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseNpgsql(
                AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ComplianceServiceCollectionExtensions.SchemaName))
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new ComplianceDbContext(options, tenantContext);
    }

    /// <summary>
    /// The module exactly as the gateway composes it (<c>AddCompliance</c>), with the tenant taken
    /// from <see cref="AmbientTenant"/> — what a request's JWT supplies in production, and what the
    /// polling consumer pushes for a handler scope. The outbox dispatcher (RabbitMQ) and the
    /// retention worker are dropped; no hosted service is ever started — tests drive one poll.
    /// </summary>
    public ServiceProvider BuildModule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext, AmbientTenantContext>();
        services.AddScoped<ISender, Sender>();
        services.AddSingleton(new OutboxPollingOptions());
        services.AddSingleton(new ProjectionOptions());
        services.AddCompliance(new ConfigurationBuilder().Build());

        // The typed registrations (AddHostedService<T>) are the dispatcher and the retention
        // worker; the consumer and the projection worker are factory registrations. Keep those.
        var factoryHosted = services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationFactory is not null).ToList();
        services.RemoveAll<IHostedService>();
        foreach (var descriptor in factoryHosted)
        {
            services.Add(descriptor);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>Runs one poll of the module's real consumer (status-tracked and fan-out paths).</summary>
    public static Task PollOnceAsync(ServiceProvider module) =>
        module.GetServices<IHostedService>().OfType<OutboxPollingConsumer<ComplianceDbContext>>().Single()
            .ProcessOnceAsync(CancellationToken.None);

    /// <summary>Runs <paramref name="act"/> in a fresh scope under <paramref name="tenantId"/>, as one request would.</summary>
    public static async Task<T> AsTenantAsync<T>(ServiceProvider module, Guid tenantId, Func<IServiceProvider, Task<T>> act)
    {
        using (AmbientTenant.Push(tenantId))
        {
            using var scope = module.CreateScope();
            return await act(scope.ServiceProvider);
        }
    }

    /// <summary>Writes the row a producer's ModuleDbContext writes: the wire JSON, the routing key precomputed.</summary>
    public async Task SeedProducerOutboxAsync(string schema, Guid tenantId, IIntegrationEvent integrationEvent)
    {
        await using var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            SELECT set_config('app.tenant_id', @tenant, false);
            INSERT INTO {schema}.outbox_messages (id, tenant_id, event_type, routing_key, payload, occurred_at_utc, created_at_utc, attempts)
            VALUES (@id, @tenantId, @type, @key, @payload::jsonb, now(), now(), 0);
            """, connection);
        command.Parameters.AddWithValue("tenant", tenantId.ToString());
        command.Parameters.AddWithValue("id", integrationEvent.EventId);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("type", integrationEvent.GetType().Name);
        command.Parameters.AddWithValue("key", EventRoutingKey.For(integrationEvent.GetType()));
        command.Parameters.AddWithValue("payload", IntegrationEventJson.Serialize(integrationEvent));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>A scalar read on a system session (cross-tenant, as an operator or the consumer would).</summary>
    public async Task<T?> SystemScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT set_config('app.is_system', 'true', false); {sql}", connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await using var reader = await command.ExecuteReaderAsync();
        await reader.NextResultAsync();
        return await reader.ReadAsync() && !await reader.IsDBNullAsync(0) ? reader.GetFieldValue<T>(0) : default;
    }

    public sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;

        public TenantType? TenantType => tenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }

    public sealed class AmbientTenantContext : ITenantContext
    {
        public Guid? TenantId => AmbientTenant.Current;

        public TenantType? TenantType => TenantId is null ? null : Shared.Kernel.TenantType.Internal;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
