using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Trips;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// The billable feed's new consumer (replacing the trip-completed one — a trip now only reaches
/// Completed once payment lands, far too late to start a worksheet). Same contract: insert-if-
/// absent on TripId, row lands uninvoiced.
/// </summary>
public class TripReadyForBillingIntegrationEventHandlerTests
{
    private static TripReadyForBillingIntegrationEvent Event(Guid tripId, bool isEmptyLeg = false) => new(
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
        "Outbound",
        isEmptyLeg,
        "PO-7781",
        new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handling_the_same_event_twice_inserts_one_row()
    {
        var repository = new InMemoryBillableTripRepository();
        var handler = new TripReadyForBillingIntegrationEventHandler(
            repository, NullLogger<TripReadyForBillingIntegrationEventHandler>.Instance);
        var tripId = Guid.NewGuid();
        var integrationEvent = Event(tripId);

        await handler.Handle(integrationEvent, CancellationToken.None);
        await handler.Handle(integrationEvent, CancellationToken.None);

        var trip = Assert.Single(repository.Trips);
        Assert.Equal(tripId, trip.Id);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task The_recorded_trip_lands_uninvoiced_with_the_operational_finish_time()
    {
        var repository = new InMemoryBillableTripRepository();
        var handler = new TripReadyForBillingIntegrationEventHandler(
            repository, NullLogger<TripReadyForBillingIntegrationEventHandler>.Instance);
        var tripId = Guid.NewGuid();

        await handler.Handle(Event(tripId, isEmptyLeg: true), CancellationToken.None);

        var trip = Assert.Single(repository.Trips);
        Assert.Equal(TestBilling.TenantId, trip.TenantId);
        Assert.Equal(TestBilling.ClientId, trip.ClientId);
        Assert.Equal("TR-4821", trip.TripNumber);
        Assert.Equal("rt-1", trip.RoundTripKey);
        Assert.Equal("Outbound", trip.Direction);
        Assert.True(trip.IsEmptyLeg);
        Assert.Null(trip.InvoiceId);
        // The replica's completed_at_utc column carries when the RUN ended — the ordering key
        // for draft lines — not when the money arrived.
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero), trip.CompletedAtUtc);
    }

    [Fact]
    public async Task Both_feed_handlers_absorb_each_others_insert()
    {
        // trip-completed and trip-ready-for-billing run side by side for one release while the
        // old key drains; a trip announced on both keys must still land exactly once.
        var repository = new InMemoryBillableTripRepository();
        var oldHandler = new TripCompletedIntegrationEventHandler(
            repository, NullLogger<TripCompletedIntegrationEventHandler>.Instance);
        var newHandler = new TripReadyForBillingIntegrationEventHandler(
            repository, NullLogger<TripReadyForBillingIntegrationEventHandler>.Instance);
        var tripId = Guid.NewGuid();

        await newHandler.Handle(Event(tripId), CancellationToken.None);
        await oldHandler.Handle(
            new TripCompletedIntegrationEvent(
                tripId, TestBilling.TenantId, "TR-4821", TestBilling.ClientId, "Lynn Lake Mining Co.",
                "ContractCrew", "Thompson–Lynn Lake", "Thompson", "Lynn Lake", 320,
                new DateOnly(2026, 7, 6), "rt-1", "Outbound", false, "PO-7781",
                new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.Single(repository.Trips);
    }
}
