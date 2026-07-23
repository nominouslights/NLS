using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Trips;
using Xunit;

namespace NorthernLink.Billing.Tests;

public class TripCompletedIntegrationEventHandlerTests
{
    private static TripCompletedIntegrationEvent Event(Guid tripId) => new(
        tripId,
        TestBilling.TenantId,
        "TR-4821",
        TestBilling.ClientId,
        "Lynn Lake Mining Co.",
        "ContractCrew",
        "Thompson–Lynn Lake",
        "Thompson",
        "Lynn Lake",
        320,
        new DateOnly(2026, 7, 6),
        "rt-1",
        "PO-7781",
        new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handling_the_same_event_twice_inserts_one_row()
    {
        var repository = new InMemoryBillableTripRepository();
        var handler = new TripCompletedIntegrationEventHandler(
            repository, NullLogger<TripCompletedIntegrationEventHandler>.Instance);
        var tripId = Guid.NewGuid();
        var integrationEvent = Event(tripId);

        await handler.Handle(integrationEvent, CancellationToken.None);
        await handler.Handle(integrationEvent, CancellationToken.None);

        var trip = Assert.Single(repository.Trips);
        Assert.Equal(tripId, trip.Id);
        Assert.Equal(TestBilling.TenantId, trip.TenantId);
        Assert.Equal("TR-4821", trip.TripNumber);
        Assert.Equal("rt-1", trip.RoundTripKey);
        Assert.Null(trip.InvoiceId);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task The_recorded_trip_lands_uninvoiced_with_the_event_fields()
    {
        var repository = new InMemoryBillableTripRepository();
        var handler = new TripCompletedIntegrationEventHandler(
            repository, NullLogger<TripCompletedIntegrationEventHandler>.Instance);
        var tripId = Guid.NewGuid();

        await handler.Handle(Event(tripId), CancellationToken.None);

        var trip = Assert.Single(repository.Trips);
        Assert.Equal("Thompson–Lynn Lake", trip.RouteName);
        Assert.Equal("Thompson", trip.Origin);
        Assert.Equal("Lynn Lake", trip.Destination);
        Assert.Equal(320, trip.DistanceKm);
        Assert.Equal(new DateOnly(2026, 7, 6), trip.ServiceDate);
        Assert.Equal("PO-7781", trip.PoNumber);
        Assert.True(trip.IsUninvoiced);
    }
}
