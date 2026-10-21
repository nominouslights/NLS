using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Shared.IntegrationEvents.Trips;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// A reopened trip is back in operation, so it leaves the uninvoiced pool; finishing it again
/// re-adds a fresh row through the ordinary insert-if-absent ready-for-billing handler.
/// </summary>
public class TripReopenedIntegrationEventHandlerTests
{
    private static TripReopenedIntegrationEvent Event(Guid tripId) => new(
        tripId, TestBilling.TenantId, "TR-4821", "Finished before the return leg ran", "ReadyForBilling", "InProgress");

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

    private static TripReopenedIntegrationEventHandler Handler(InMemoryBillableTripRepository repository) =>
        new(repository, NullLogger<TripReopenedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task An_uninvoiced_billable_trip_is_removed_from_the_pool()
    {
        var repository = new InMemoryBillableTripRepository();
        var tripId = Guid.NewGuid();
        repository.Trips.Add(Row(tripId));

        await Handler(repository).Handle(Event(tripId), CancellationToken.None);

        Assert.Empty(repository.Trips);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task A_claimed_row_is_kept_because_the_claim_is_the_stronger_fact()
    {
        var repository = new InMemoryBillableTripRepository();
        var tripId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        repository.Trips.Add(Row(tripId, invoiceId));

        await Handler(repository).Handle(Event(tripId), CancellationToken.None);

        var kept = Assert.Single(repository.Trips);
        Assert.Equal(invoiceId, kept.InvoiceId);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task An_absent_row_is_an_idempotent_no_op()
    {
        var repository = new InMemoryBillableTripRepository();
        var tripId = Guid.NewGuid();
        repository.Trips.Add(Row(tripId));
        var handler = Handler(repository);

        await handler.Handle(Event(tripId), CancellationToken.None);
        await handler.Handle(Event(tripId), CancellationToken.None); // redelivery

        Assert.Empty(repository.Trips);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Finishing_again_after_a_reopen_re_adds_a_fresh_row()
    {
        var repository = new InMemoryBillableTripRepository();
        var tripId = Guid.NewGuid();
        repository.Trips.Add(Row(tripId));

        await Handler(repository).Handle(Event(tripId), CancellationToken.None);

        var finishedAgainAt = new DateTimeOffset(2026, 7, 7, 9, 0, 0, TimeSpan.Zero);
        var readyAgain = new TripReadyForBillingIntegrationEvent(
            tripId, TestBilling.TenantId, "TR-4821", TestBilling.ClientId, "Lynn Lake Mining Co.", "ContractCrew",
            "Thompson–Lynn Lake", "Thompson", "Lynn Lake", 320, new DateOnly(2026, 7, 6),
            null, null, false, null, finishedAgainAt);
        await new TripReadyForBillingIntegrationEventHandler(
                repository, NullLogger<TripReadyForBillingIntegrationEventHandler>.Instance)
            .Handle(readyAgain, CancellationToken.None);

        var row = Assert.Single(repository.Trips);
        Assert.Equal(tripId, row.Id);
        Assert.Null(row.InvoiceId);
        Assert.Equal(finishedAgainAt, row.CompletedAtUtc);
    }
}
