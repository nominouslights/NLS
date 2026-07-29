using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Trips;
using Xunit;

namespace NorthernLink.Billing.Tests;

public class TripRoundTripChangedIntegrationEventHandlerTests
{
    private static TripRoundTripChangedIntegrationEventHandler Handler(InMemoryBillableTripRepository repository) =>
        new(repository, NullLogger<TripRoundTripChangedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task An_uninvoiced_row_is_re_keyed()
    {
        var repository = new InMemoryBillableTripRepository();
        var trip = TestBilling.Trip(new DateOnly(2026, 7, 6), roundTripKey: null);
        repository.Add(trip);

        await Handler(repository).Handle(
            new TripRoundTripChangedIntegrationEvent(trip.Id, TestBilling.TenantId, "merge:abc", "Outbound"),
            CancellationToken.None);

        Assert.Equal("merge:abc", trip.RoundTripKey);
        Assert.Equal("Outbound", trip.Direction);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task An_unpair_clears_the_row_key_and_direction()
    {
        var repository = new InMemoryBillableTripRepository();
        var trip = TestBilling.Trip(new DateOnly(2026, 7, 6), "merge:abc", direction: "Inbound");
        repository.Add(trip);

        await Handler(repository).Handle(
            new TripRoundTripChangedIntegrationEvent(trip.Id, TestBilling.TenantId, null, null),
            CancellationToken.None);

        Assert.Null(trip.RoundTripKey);
        Assert.Null(trip.Direction);
    }

    [Fact]
    public async Task An_invoiced_row_is_left_alone()
    {
        var repository = new InMemoryBillableTripRepository();
        var claimed = TestBilling.Trip(new DateOnly(2026, 7, 6), "rt-1", direction: "Outbound",
            invoiceId: Guid.NewGuid());
        repository.Add(claimed);

        await Handler(repository).Handle(
            new TripRoundTripChangedIntegrationEvent(claimed.Id, TestBilling.TenantId, "merge:abc", "Inbound"),
            CancellationToken.None);

        Assert.Equal("rt-1", claimed.RoundTripKey);
        Assert.Equal("Outbound", claimed.Direction);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task A_missing_row_is_skipped_without_saving()
    {
        var repository = new InMemoryBillableTripRepository();

        await Handler(repository).Handle(
            new TripRoundTripChangedIntegrationEvent(Guid.NewGuid(), TestBilling.TenantId, "merge:abc", "Outbound"),
            CancellationToken.None);

        Assert.Empty(repository.Trips);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Redelivery_converges_on_the_same_values()
    {
        var repository = new InMemoryBillableTripRepository();
        var trip = TestBilling.Trip(new DateOnly(2026, 7, 6), roundTripKey: null);
        repository.Add(trip);
        var integrationEvent = new TripRoundTripChangedIntegrationEvent(
            trip.Id, TestBilling.TenantId, "merge:abc", "Outbound");

        await Handler(repository).Handle(integrationEvent, CancellationToken.None);
        await Handler(repository).Handle(integrationEvent, CancellationToken.None);

        Assert.Equal("merge:abc", trip.RoundTripKey);
        Assert.Equal("Outbound", trip.Direction);
    }
}
