using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Clients;
using Xunit;

namespace NorthernLink.Billing.Tests;

/// <summary>
/// The <c>purchase_order_snapshots</c> replica: upsert keyed on PurchaseOrderId so replay
/// converges, and a delete that actually drops the row (a PO has no lifecycle and is genuinely
/// removable, unlike a contract).
/// </summary>
public class PurchaseOrderSnapshotIntegrationEventHandlerTests
{
    private static PurchaseOrderChangedIntegrationEvent Changed(
        Guid purchaseOrderId,
        string poNumber = "PO-7781",
        decimal? amountCad = 250_000m,
        decimal? roundTripRateCad = 300m,
        decimal? oneWayRateCad = 180m,
        DateOnly? expiry = null) => new(
        purchaseOrderId,
        TestBilling.TenantId,
        TestBilling.ClientId,
        poNumber,
        new DateOnly(2026, 1, 15),
        expiry,
        amountCad,
        roundTripRateCad,
        oneWayRateCad);

    private static PurchaseOrderChangedIntegrationEventHandler ChangedHandler(
        InMemoryPurchaseOrderSnapshotRepository repository) =>
        new(repository, NullLogger<PurchaseOrderChangedIntegrationEventHandler>.Instance);

    private static PurchaseOrderDeletedIntegrationEventHandler DeletedHandler(
        InMemoryPurchaseOrderSnapshotRepository repository) =>
        new(repository, NullLogger<PurchaseOrderDeletedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task Replaying_the_same_changed_event_converges_on_one_row()
    {
        var repository = new InMemoryPurchaseOrderSnapshotRepository();
        var handler = ChangedHandler(repository);
        var purchaseOrderId = Guid.NewGuid();
        var integrationEvent = Changed(purchaseOrderId);

        await handler.Handle(integrationEvent, CancellationToken.None);
        await handler.Handle(integrationEvent, CancellationToken.None);
        await handler.Handle(integrationEvent, CancellationToken.None);

        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal(purchaseOrderId, snapshot.Id);
        Assert.Equal(TestBilling.TenantId, snapshot.TenantId);
        Assert.Equal(TestBilling.ClientId, snapshot.ClientId);
        Assert.Equal("PO-7781", snapshot.PoNumber);
        Assert.Equal(250_000m, snapshot.AmountCad);
        Assert.Equal(300m, snapshot.RoundTripRateCad);
        Assert.Equal(180m, snapshot.OneWayRateCad);
    }

    [Fact]
    public async Task A_later_event_for_the_same_po_updates_the_row_in_place()
    {
        var repository = new InMemoryPurchaseOrderSnapshotRepository();
        var handler = ChangedHandler(repository);
        var purchaseOrderId = Guid.NewGuid();

        await handler.Handle(Changed(purchaseOrderId, roundTripRateCad: 300m), CancellationToken.None);
        await handler.Handle(
            Changed(purchaseOrderId, poNumber: "PO-9000", roundTripRateCad: 355m, oneWayRateCad: null),
            CancellationToken.None);

        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal("PO-9000", snapshot.PoNumber);
        Assert.Equal(355m, snapshot.RoundTripRateCad);
        // Clearing a term on the write side clears it on the replica — it never sticks.
        Assert.Null(snapshot.OneWayRateCad);
    }

    [Fact]
    public async Task A_po_with_no_terms_replicates_as_two_nulls()
    {
        var repository = new InMemoryPurchaseOrderSnapshotRepository();

        await ChangedHandler(repository).Handle(
            Changed(Guid.NewGuid(), roundTripRateCad: null, oneWayRateCad: null), CancellationToken.None);

        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Null(snapshot.RoundTripRateCad);
        Assert.Null(snapshot.OneWayRateCad);
    }

    [Fact]
    public async Task Deleting_drops_the_snapshot()
    {
        var repository = new InMemoryPurchaseOrderSnapshotRepository();
        var purchaseOrderId = Guid.NewGuid();
        await ChangedHandler(repository).Handle(Changed(purchaseOrderId), CancellationToken.None);
        Assert.Single(repository.Snapshots);

        await DeletedHandler(repository).Handle(
            new PurchaseOrderDeletedIntegrationEvent(purchaseOrderId, TestBilling.TenantId),
            CancellationToken.None);

        Assert.Empty(repository.Snapshots);
    }

    [Fact]
    public async Task Replaying_a_delete_for_an_already_gone_po_is_a_no_op()
    {
        var repository = new InMemoryPurchaseOrderSnapshotRepository();
        var purchaseOrderId = Guid.NewGuid();
        await ChangedHandler(repository).Handle(Changed(purchaseOrderId), CancellationToken.None);
        var deletedEvent = new PurchaseOrderDeletedIntegrationEvent(purchaseOrderId, TestBilling.TenantId);

        await DeletedHandler(repository).Handle(deletedEvent, CancellationToken.None);
        await DeletedHandler(repository).Handle(deletedEvent, CancellationToken.None);

        Assert.Empty(repository.Snapshots);
    }

    [Fact]
    public async Task A_delete_leaves_another_tenants_row_alone()
    {
        var repository = new InMemoryPurchaseOrderSnapshotRepository();
        var purchaseOrderId = Guid.NewGuid();
        await ChangedHandler(repository).Handle(Changed(purchaseOrderId), CancellationToken.None);

        await DeletedHandler(repository).Handle(
            new PurchaseOrderDeletedIntegrationEvent(purchaseOrderId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Single(repository.Snapshots);
    }
}
