using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Drives the storing/projecting delivery path end-to-end against a real Postgres as the
/// non-superuser app role: aggregate save → outbox row (same transaction) → polling
/// consumer reads the schema cross-tenant under <c>app.is_system</c> → handlers run →
/// status column advances. Self-contained on the fleet schema (the consumer under test
/// polls <c>fleet</c> as its producer), so no second module is needed.
/// </summary>
[Collection("postgres")]
public class OutboxPollingConsumerTests(PostgresFixture fixture)
{
    private sealed class RecordingHandler : IIntegrationEventHandler<VehicleChangedIntegrationEvent>
    {
        public List<VehicleChangedIntegrationEvent> Handled { get; } = [];

        public Task Handle(VehicleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Handled.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IIntegrationEventHandler<VehicleChangedIntegrationEvent>
    {
        public int Invocations { get; private set; }

        public Task Handle(VehicleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Invocations++;
            throw new InvalidOperationException("handler failure (test)");
        }
    }

    /// <summary>Fails only the vehicle-register event ("Active"); the status-change event succeeds.</summary>
    private sealed class FailOnActiveHandler : IIntegrationEventHandler<VehicleChangedIntegrationEvent>
    {
        public List<VehicleChangedIntegrationEvent> Handled { get; } = [];

        public Task Handle(VehicleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            if (integrationEvent.Status == "Active")
            {
                throw new InvalidOperationException("poison row (test)");
            }

            Handled.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Pending_rows_are_processed_in_order_across_tenants_even_when_already_dispatched()
    {
        await MarkAllProcessedAsync(); // isolate from rows left behind by earlier tests

        var vehicleA = await SaveVehicleWithStatusChangeAsync(PostgresFixture.TenantA);
        var vehicleB = await SaveVehicleWithStatusChangeAsync(PostgresFixture.TenantB);

        // Simulate history the old RabbitMQ path already published: dispatched rows must
        // STILL be processed — the status column, not dispatched_at_utc, is the delivery
        // state, which is exactly what replays missed events after the migration.
        await ExecuteSystemSqlAsync(
            "UPDATE fleet.outbox_messages SET dispatched_at_utc = now() WHERE processing_status = 'Pending'");

        var handler = new RecordingHandler();
        var (consumer, provider) = BuildConsumer(handler);
        await using (provider)
        {
            await consumer.ProcessOnceAsync(CancellationToken.None);

            var handledForA = handler.Handled.Where(e => e.VehicleId == vehicleA.Id).ToList();
            var handledForB = handler.Handled.Where(e => e.VehicleId == vehicleB.Id).ToList();
            Assert.Equal(new[] { "Active", "InMaintenance" }, handledForA.Select(e => e.Status)); // position order
            Assert.Equal(new[] { "Active", "InMaintenance" }, handledForB.Select(e => e.Status)); // both tenants, one system poll
            Assert.Equal(PostgresFixture.TenantA, Assert.Single(handledForA, e => e.Status == "Active").TenantId);

            await using var context = fixture.CreateContext(PostgresFixture.TenantA);
            var rows = await RowsForAsync(context, vehicleA.Id);
            Assert.NotEmpty(rows);
            Assert.All(rows, row =>
            {
                Assert.Equal(OutboxProcessingStatus.Processed, row.ProcessingStatus);
                Assert.NotNull(row.ProcessedAtUtc);
            });

            // A second poll finds nothing Pending — no reprocessing.
            var alreadyHandled = handler.Handled.Count;
            await consumer.ProcessOnceAsync(CancellationToken.None);
            Assert.Equal(alreadyHandled, handler.Handled.Count);
        }
    }

    [Fact]
    public async Task Failure_records_attempts_and_backoff_and_blocks_its_schema_for_the_tick()
    {
        await MarkAllProcessedAsync();

        var vehicle = await SaveVehicleWithStatusChangeAsync(PostgresFixture.TenantA);

        var handler = new FailingHandler();
        var (consumer, provider) = BuildConsumer(handler);
        await using (provider)
        {
            await consumer.ProcessOnceAsync(CancellationToken.None);

            // Only the head row was attempted; the row behind it stays untouched so
            // position order holds while the head is in backoff.
            Assert.Equal(1, handler.Invocations);

            await using var context = fixture.CreateContext(PostgresFixture.TenantA);
            var rows = await RowsForAsync(context, vehicle.Id);
            var head = rows.First();
            Assert.Equal(OutboxProcessingStatus.Pending, head.ProcessingStatus);
            Assert.Equal(1, head.ProcessingAttempts);
            Assert.Contains("handler failure", head.ProcessingLastError);
            Assert.NotNull(head.ProcessingNextAttemptAtUtc);
            Assert.All(rows.Skip(1), row =>
            {
                Assert.Equal(OutboxProcessingStatus.Pending, row.ProcessingStatus);
                Assert.Equal(0, row.ProcessingAttempts);
            });

            // While the backoff gate is in the future the schema stays blocked.
            await consumer.ProcessOnceAsync(CancellationToken.None);
            Assert.Equal(1, handler.Invocations);
        }
    }

    [Fact]
    public async Task Exhausted_row_is_parked_as_failed_and_later_rows_flow()
    {
        await MarkAllProcessedAsync();

        var vehicle = await SaveVehicleWithStatusChangeAsync(PostgresFixture.TenantA);

        // MaxAttempts = 1: the poison register row ("Active") parks on its first failure,
        // and the status-change row behind it is processed in the same poll.
        var handler = new FailOnActiveHandler();
        var (consumer, provider) = BuildConsumer(handler, new OutboxPollingOptions { MaxAttempts = 1 });
        await using (provider)
        {
            await consumer.ProcessOnceAsync(CancellationToken.None);

            Assert.Equal("InMaintenance", Assert.Single(handler.Handled, e => e.VehicleId == vehicle.Id).Status);

            await using var context = fixture.CreateContext(PostgresFixture.TenantA);
            var rows = await RowsForAsync(context, vehicle.Id);
            var parked = rows.First();
            Assert.Equal(OutboxProcessingStatus.Failed, parked.ProcessingStatus);
            Assert.Contains("poison row", parked.ProcessingLastError);
            Assert.Equal(OutboxProcessingStatus.Processed, rows.Last().ProcessingStatus);
        }
    }

    private async Task<Vehicle> SaveVehicleWithStatusChangeAsync(Guid tenantId)
    {
        var vehicle = TestVehicleFactory.Create(tenantId);
        await using var context = fixture.CreateContext(tenantId, withMapper: true);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        Assert.True(vehicle.ChangeStatus(VehicleStatus.InMaintenance, "Polling test").IsSuccess);
        await context.SaveChangesAsync();
        return vehicle;
    }

    /// <summary>This test's rows, position-ordered: the vehicle-changed events for one vehicle.</summary>
    private static async Task<List<OutboxMessage>> RowsForAsync(FleetDbContext context, Guid vehicleId)
    {
        var rows = await context.Set<OutboxMessage>()
            .Where(m => m.RoutingKey == "fleet.vehicle-changed")
            .OrderBy(m => m.Position)
            .ToListAsync();
        return rows.Where(m => m.Payload.Contains(vehicleId.ToString())).ToList();
    }

    /// <summary>Baseline: mark every leftover row Processed so each test only sees its own.</summary>
    private Task MarkAllProcessedAsync() => ExecuteSystemSqlAsync(
        "UPDATE fleet.outbox_messages SET processing_status = 'Processed', processed_at_utc = now() WHERE processing_status = 'Pending'");

    private async Task ExecuteSystemSqlAsync(string sql)
    {
        await using var connection = await fixture.OpenRawConnectionAsync();
        await using (var system = new NpgsqlCommand("SELECT set_config('app.is_system', 'true', false);", connection))
        {
            await system.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The real consumer polling the fleet schema, wired like AddOutboxPollingConsumer.</summary>
    private (OutboxPollingConsumer<FleetDbContext> Consumer, ServiceProvider Provider) BuildConsumer(
        IIntegrationEventHandler<VehicleChangedIntegrationEvent> handler,
        OutboxPollingOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext>(_ => new PostgresFixture.TestTenantContext(null));
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<FleetDbContext>((provider, builder) => builder
            .UseNpgsql(
                fixture.AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>()));
        services.AddSingleton(handler);

        var subscriptions = new IntegrationEventSubscriptions("fleet-polling-test");
        subscriptions.Add(typeof(VehicleChangedIntegrationEvent));

        var provider = services.BuildServiceProvider();
        var consumer = new OutboxPollingConsumer<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            subscriptions,
            options ?? new OutboxPollingOptions(),
            NullLogger<OutboxPollingConsumer<FleetDbContext>>.Instance);

        return (consumer, provider);
    }
}
