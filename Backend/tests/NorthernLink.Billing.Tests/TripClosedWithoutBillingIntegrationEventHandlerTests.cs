using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Shared.IntegrationEvents.Trips;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// The undo of the billable feed: a dispatcher closed the trip without billing, so it leaves
/// the pool for good. Deletion, not a flag — absence is what "not billable" means here.
/// </summary>
public class TripClosedWithoutBillingIntegrationEventHandlerTests
{
    private static TripClosedWithoutBillingIntegrationEvent Event(Guid tripId) => new(
        tripId, TestBilling.TenantId, "TR-4821", "Client has no active contract");

    private static BillableTrip Row(Guid tripId, Guid? invoiceId = null) => new()
    {
        Id = tripId,
        TenantId = TestBilling.TenantId,
        TripNumber = "TR-4821",
        ClientId = TestBilling.ClientId,
        ClientName = "Lynn Lake Mining Co.",
        ServiceType = "ContractCrew",
        RouteName = "Thompson–Lynn Lake",
        Origin = "Thompson",
        Destination = "Lynn Lake",
        DistanceKm = 320,
        ServiceDate = new DateOnly(2026, 7, 6),
        CompletedAtUtc = new DateTimeOffset(2026, 7, 6, 18, 0, 0, TimeSpan.Zero),
        InvoiceId = invoiceId,
    };

    [Fact]
    public async Task An_uninvoiced_billable_trip_is_removed_from_the_pool()
    {
        var repository = new InMemoryBillableTripRepository();
        var tripId = Guid.NewGuid();
        repository.Trips.Add(Row(tripId));
        var handler = new TripClosedWithoutBillingIntegrationEventHandler(
            repository, NullLogger<TripClosedWithoutBillingIntegrationEventHandler>.Instance);

        await handler.Handle(Event(tripId), CancellationToken.None);

        Assert.Empty(repository.Trips);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task An_absent_row_is_an_idempotent_no_op()
    {
        var repository = new InMemoryBillableTripRepository();
        var handler = new TripClosedWithoutBillingIntegrationEventHandler(
            repository, NullLogger<TripClosedWithoutBillingIntegrationEventHandler>.Instance);

        await handler.Handle(Event(Guid.NewGuid()), CancellationToken.None); // redelivery shape

        Assert.Empty(repository.Trips);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task A_claimed_row_is_kept_because_the_claim_is_the_stronger_fact()
    {
        var repository = new InMemoryBillableTripRepository();
        var tripId = Guid.NewGuid();
        repository.Trips.Add(Row(tripId, invoiceId: Guid.NewGuid()));
        var handler = new TripClosedWithoutBillingIntegrationEventHandler(
            repository, NullLogger<TripClosedWithoutBillingIntegrationEventHandler>.Instance);

        // The producer refuses to close a claimed trip, so reaching this means a close raced a
        // draft — the worksheet claim wins, and the row stays.
        await handler.Handle(Event(tripId), CancellationToken.None);

        Assert.Single(repository.Trips);
        Assert.Equal(0, repository.SaveCount);
    }
}
