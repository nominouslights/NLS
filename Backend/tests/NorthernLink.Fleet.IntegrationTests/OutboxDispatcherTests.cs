using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

[Collection("postgres")]
public class OutboxDispatcherTests(PostgresFixture fixture)
{
    private sealed class RecordingTransport : IOutboxTransport
    {
        public List<(string RoutingKey, string Body)> Published { get; } = [];

        public Task Publish(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            Published.Add((routingKey, Encoding.UTF8.GetString(body.Span)));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTransport : IOutboxTransport
    {
        public Task Publish(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("broker unavailable (test)");
    }

    [Fact]
    public async Task Status_change_is_outboxed_and_dispatched_to_the_transport()
    {
        var vehicle = await SaveVehicleWithStatusChangeAsync();

        // The outbox row was written in the same transaction as the status change.
        await using (var reader = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var message = Assert.Single(await PendingRowsForAsync(reader, vehicle.Id));
            Assert.Equal("fleet.vehicle-status-changed", message.RoutingKey);
            Assert.Equal("VehicleStatusChangedIntegrationEvent", message.EventType);
        }

        var transport = new RecordingTransport();
        await RunDispatcherUntilAsync(transport, async () =>
        {
            await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
            return (await PendingRowsForAsync(reader, vehicle.Id)).Count == 0;
        });

        var published = Assert.Single(transport.Published, p => p.Body.Contains(vehicle.Id.ToString()));
        Assert.Equal("fleet.vehicle-status-changed", published.RoutingKey);
        Assert.Contains(PostgresFixture.TenantA.ToString(), published.Body);
    }

    [Fact]
    public async Task Failed_publish_records_attempt_error_and_backoff()
    {
        var vehicle = await SaveVehicleWithStatusChangeAsync();

        await RunDispatcherUntilAsync(new FailingTransport(), async () =>
        {
            await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
            var rows = await PendingRowsForAsync(reader, vehicle.Id);
            return rows.Count == 1 && rows[0].Attempts >= 1;
        });

        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var message = Assert.Single(await PendingRowsForAsync(context, vehicle.Id));
        Assert.Null(message.DispatchedAtUtc);
        Assert.True(message.Attempts >= 1);
        Assert.Contains("broker unavailable", message.LastError);
        Assert.NotNull(message.NextAttemptAtUtc);
    }

    private async Task<Vehicle> SaveVehicleWithStatusChangeAsync()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);
        await using var context = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        Assert.True(vehicle.ChangeStatus(VehicleStatus.InMaintenance, "Dispatch test").IsSuccess);
        await context.SaveChangesAsync();
        return vehicle;
    }

    private static async Task<List<OutboxMessage>> PendingRowsForAsync(FleetDbContext context, Guid vehicleId)
    {
        var rows = await context.Set<OutboxMessage>()
            .Where(m => m.DispatchedAtUtc == null)
            .ToListAsync();
        return rows.Where(m => m.Payload.Contains(vehicleId.ToString())).ToList();
    }

    /// <summary>Runs the real hosted dispatcher with a fast poll until the condition holds (or 30 s).</summary>
    private async Task RunDispatcherUntilAsync(IOutboxTransport transport, Func<Task<bool>> condition)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext>(_ => new PostgresFixture.TestTenantContext(null));
        services.AddScoped<TenantSessionInterceptor>();
        services.AddScoped<FleetIntegrationEventMapper>();
        services.AddDbContext<FleetDbContext>((provider, options) => options
            .UseNpgsql(
                fixture.AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>()));

        await using var provider = services.BuildServiceProvider();
        var dispatcher = new OutboxDispatcher<FleetDbContext>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            transport,
            new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(100) },
            NullLogger<OutboxDispatcher<FleetDbContext>>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (!await condition())
            {
                Assert.True(DateTimeOffset.UtcNow < deadline, "Dispatcher did not reach the expected state in time.");
                await Task.Delay(100);
            }
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }
}
