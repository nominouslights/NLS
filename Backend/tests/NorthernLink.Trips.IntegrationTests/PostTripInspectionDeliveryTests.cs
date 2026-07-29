using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Persistence;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure;
using NorthernLink.Trips.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// The TR-3 regression: a Fleet post-trip inspection event sitting in fleet.outbox_messages
/// must flip the matching trip's HasPostTripInspection flag via the Trips polling consumer —
/// the delivery that the old RabbitMQ path silently lost. Runs the REAL production wiring:
/// AddOutboxPollingConsumer with the real handler, real repositories, real RLS.
/// </summary>
[Collection("postgres")]
public class PostTripInspectionDeliveryTests(PostgresFixture fixture)
{
    private const string TripNumber = "TR-3";

    [Fact]
    public async Task Post_trip_inspection_event_in_fleet_outbox_flips_the_trips_completion_gate()
    {
        var tripId = await SeedInProgressTripAsync(TripNumber);
        var eventId = await SeedFleetOutboxRowAsync(tripNumber: TripNumber, inspectionType: "PostTrip");
        // An ignorable event behind it — a pre-trip inspection is filtered by the handler
        // but must still be marked Processed, not block the schema.
        var preTripEventId = await SeedFleetOutboxRowAsync(tripNumber: TripNumber, inspectionType: "PreTrip");

        await RunConsumerOnceAsync();

        await using (var trips = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            var trip = await trips.Trips.SingleAsync(t => t.Id == tripId);
            Assert.True(trip.HasPostTripInspection); // Trip.Complete()'s gate is now open
        }

        await using (var fleet = fixture.CreateFleetContext(PostgresFixture.TenantA))
        {
            var rows = await fleet.Set<OutboxMessage>()
                .Where(m => m.Id == eventId || m.Id == preTripEventId)
                .ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(OutboxProcessingStatus.Processed, row.ProcessingStatus));
        }
    }

    [Fact]
    public async Task Event_with_no_matching_trip_is_processed_not_parked()
    {
        var eventId = await SeedFleetOutboxRowAsync(tripNumber: "TR-DOES-NOT-EXIST", inspectionType: "PostTrip");

        await RunConsumerOnceAsync();

        await using var fleet = fixture.CreateFleetContext(PostgresFixture.TenantA);
        var row = await fleet.Set<OutboxMessage>().SingleAsync(m => m.Id == eventId);
        // The handler logs-and-ignores unmatched trip numbers (ad-hoc inspections are
        // normal), so the row completes instead of blocking everything behind it.
        Assert.Equal(OutboxProcessingStatus.Processed, row.ProcessingStatus);
    }

    private async Task<Guid> SeedInProgressTripAsync(string tripNumber)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);

        var existing = await context.Trips.SingleOrDefaultAsync(t => t.TripNumber == tripNumber);
        if (existing is not null)
        {
            return existing.Id;
        }

        var trip = Trip.Schedule(
            PostgresFixture.TenantA,
            tripNumber,
            serviceDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            windowStart: new TimeOnly(8, 0),
            windowEnd: null,
            serviceType: TripServiceType.Charter,
            routeId: null,
            routeName: "Thompson – Lynn Lake",
            origin: "Thompson",
            destination: "Lynn Lake",
            stops: Array.Empty<RouteStop>(),
            distanceKm: 320,
            scheduleTemplateId: null,
            roundTripKey: null,
            direction: null,
            isEmptyLeg: false,
            clientId: null,
            clientName: null,
            poNumber: null,
            driverId: null,
            driverName: null,
            vehicleId: null,
            vehicleUnit: null,
            seatsCapacity: null,
            seatsMinimum: null);
        Assert.True(trip.IsSuccess, trip.IsFailure ? trip.Error.Code : "");
        Assert.True(trip.Value.Start().IsSuccess);

        context.Trips.Add(trip.Value);
        await context.SaveChangesAsync();
        return trip.Value.Id;
    }

    /// <summary>
    /// Writes the exact row Fleet's ModuleDbContext writes when an inspection is entered:
    /// the wire JSON of the integration event, routing key precomputed. Seeding the row
    /// directly keeps this test about DELIVERY (the mapper's own tests live in Fleet).
    /// </summary>
    private async Task<Guid> SeedFleetOutboxRowAsync(string tripNumber, string inspectionType)
    {
        var integrationEvent = new VehicleInspectionRecordedIntegrationEvent(
            InspectionId: Guid.NewGuid(),
            TenantId: PostgresFixture.TenantA,
            TripNumber: tripNumber,
            InspectionType: inspectionType,
            VehicleId: null,
            Source: "Dispatcher");

        await using var context = fixture.CreateFleetContext(PostgresFixture.TenantA);
        context.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            TenantId = PostgresFixture.TenantA,
            EventType = nameof(VehicleInspectionRecordedIntegrationEvent),
            RoutingKey = EventRoutingKey.For(typeof(VehicleInspectionRecordedIntegrationEvent)),
            Payload = IntegrationEventJson.Serialize(integrationEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
        return integrationEvent.EventId;
    }

    /// <summary>The production wiring: AddOutboxPollingConsumer with the real handler.</summary>
    private async Task RunConsumerOnceAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext, PostgresFixture.AmbientTenantContext>();
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<TripsDbContext>((provider, builder) => builder
            .UseNpgsql(
                fixture.AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", TripsServiceCollectionExtensions.SchemaName))
            .AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>()));
        services.AddScoped<ITripRepository, TestTripRepository>();
        services.AddSingleton(new OutboxPollingOptions());
        services.AddOutboxPollingConsumer<TripsDbContext>(
            TripsServiceCollectionExtensions.SchemaName,
            subscriptions => subscriptions
                .On<VehicleInspectionRecordedIntegrationEvent, VehicleInspectionRecordedIntegrationEventHandler>());

        await using var provider = services.BuildServiceProvider();
        var consumer = Assert.IsType<OutboxPollingConsumer<TripsDbContext>>(
            provider.GetRequiredService<IHostedService>());
        await consumer.ProcessOnceAsync(CancellationToken.None);
    }

    /// <summary>
    /// Mirrors the module's internal TripRepository over the public ITripRepository surface
    /// (the real one is internal to Trips.Infrastructure).
    /// </summary>
    private sealed class TestTripRepository(TripsDbContext context) : ITripRepository
    {
        public void Add(Trip trip) => context.Trips.Add(trip);

        public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            context.Trips.FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
            context.Trips.FirstOrDefaultAsync(t => t.TripNumber == tripNumber, cancellationToken);

        public async Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(
            string roundTripKey, CancellationToken cancellationToken = default) =>
            await context.Trips
                .Where(t => t.RoundTripKey == roundTripKey)
                .ToListAsync(cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            context.SaveChangesAsync(cancellationToken);
    }
}
