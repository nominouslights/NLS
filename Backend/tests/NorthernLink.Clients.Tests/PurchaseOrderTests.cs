using NorthernLink.Clients.Application.PurchaseOrders.Create;
using NorthernLink.Clients.Application.PurchaseOrders.Delete;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.PurchaseOrders;
using NorthernLink.Clients.Domain.PurchaseOrders.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>PurchaseOrder factory validation and create/delete handler behavior.</summary>
public class PurchaseOrderTests
{
    private static NorthernLink.Shared.Kernel.Result<PurchaseOrder> Create(
        string poNumber = "PO-88231",
        DateOnly? issued = null,
        DateOnly? expiry = null,
        decimal? amountCad = 250_000m,
        decimal? roundTripRateCad = null,
        decimal? oneWayRateCad = null) =>
        PurchaseOrder.Create(
            TestClients.TenantId,
            Guid.NewGuid(),
            poNumber,
            issued ?? new DateOnly(2026, 1, 15),
            expiry,
            amountCad,
            roundTripRateCad,
            oneWayRateCad,
            "Crew change coverage");

    [Fact]
    public void Po_number_is_required()
    {
        var result = Create(poNumber: "  ");

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.PoNumberRequired, result.Error);
    }

    [Fact]
    public void Expiry_before_issue_date_is_invalid()
    {
        var result = Create(issued: new DateOnly(2026, 6, 1), expiry: new DateOnly(2026, 5, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.InvalidExpiry, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public void Amount_when_present_must_be_positive(decimal amount)
    {
        var result = Create(amountCad: amount);

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.InvalidAmount, result.Error);
    }

    [Fact]
    public void Amount_is_optional()
    {
        var result = Create(amountCad: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AmountCad);
    }

    [Fact]
    public void Update_applies_new_details()
    {
        var purchaseOrder = Create().Value;

        var result = purchaseOrder.Update(
            "PO-99000", new DateOnly(2026, 2, 1), new DateOnly(2026, 12, 31), 300_000m, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("PO-99000", purchaseOrder.PoNumber);
        Assert.Equal(300_000m, purchaseOrder.AmountCad);
        Assert.Null(purchaseOrder.Note);
    }

    // ---- Per-PO pricing terms ----

    [Fact]
    public void A_po_may_carry_both_of_its_own_rates()
    {
        var result = Create(roundTripRateCad: 305m, oneWayRateCad: 180m);

        Assert.True(result.IsSuccess);
        Assert.Equal(305m, result.Value.RoundTripRateCad);
        Assert.Equal(180m, result.Value.OneWayRateCad);
    }

    [Fact]
    public void A_round_trip_rate_alone_is_allowed()
    {
        var result = Create(roundTripRateCad: 305m, oneWayRateCad: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(305m, result.Value.RoundTripRateCad);
        Assert.Null(result.Value.OneWayRateCad);
    }

    [Fact]
    public void A_one_way_rate_alone_is_allowed()
    {
        var result = Create(roundTripRateCad: null, oneWayRateCad: 180m);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.RoundTripRateCad);
        Assert.Equal(180m, result.Value.OneWayRateCad);
    }

    [Fact]
    public void Neither_rate_is_required()
    {
        var result = Create(roundTripRateCad: null, oneWayRateCad: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.RoundTripRateCad);
        Assert.Null(result.Value.OneWayRateCad);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-120)]
    public void Round_trip_rate_when_present_must_be_positive(decimal rate)
    {
        var result = Create(roundTripRateCad: rate);

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.InvalidRoundTripRate, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void One_way_rate_when_present_must_be_positive(decimal rate)
    {
        var result = Create(oneWayRateCad: rate);

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.InvalidOneWayRate, result.Error);
    }

    /// <summary>
    /// A one-way rate is an absolute negotiated figure, not a fraction — nothing requires it to
    /// be half of, or even less than, the round-trip rate.
    /// </summary>
    [Fact]
    public void A_one_way_rate_above_half_the_round_trip_rate_is_allowed()
    {
        var result = Create(roundTripRateCad: 300m, oneWayRateCad: 250m);

        Assert.True(result.IsSuccess);
        Assert.Equal(250m, result.Value.OneWayRateCad);
    }

    [Fact]
    public void Update_replaces_the_rates_and_a_null_clears_one()
    {
        var purchaseOrder = Create(roundTripRateCad: 305m, oneWayRateCad: 180m).Value;

        var result = purchaseOrder.Update(
            "PO-88231", new DateOnly(2026, 1, 15), null, 250_000m,
            roundTripRateCad: 355m, oneWayRateCad: null, note: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(355m, purchaseOrder.RoundTripRateCad);
        Assert.Null(purchaseOrder.OneWayRateCad);
    }

    [Fact]
    public void Update_rejects_a_non_positive_rate_and_leaves_the_po_untouched()
    {
        var purchaseOrder = Create(roundTripRateCad: 305m).Value;

        var result = purchaseOrder.Update(
            "PO-88231", new DateOnly(2026, 1, 15), null, 250_000m,
            roundTripRateCad: -1m, oneWayRateCad: null, note: null);

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.InvalidRoundTripRate, result.Error);
        Assert.Equal(305m, purchaseOrder.RoundTripRateCad);
    }

    [Fact]
    public void Create_and_update_round_trip_the_rates_through_the_handlers()
    {
        var purchaseOrder = Create(roundTripRateCad: 305m, oneWayRateCad: 180m).Value;

        Assert.True(purchaseOrder
            .Update("PO-88231", purchaseOrder.Issued, null, 250_000m, 400m, 220m, null)
            .IsSuccess);
        Assert.Equal(400m, purchaseOrder.RoundTripRateCad);
        Assert.Equal(220m, purchaseOrder.OneWayRateCad);
    }

    [Fact]
    public void Marking_a_po_deleted_raises_the_removal_event_so_billing_can_drop_its_replica()
    {
        var purchaseOrder = Create().Value;
        purchaseOrder.ClearDomainEvents();

        purchaseOrder.MarkDeleted();

        var domainEvent = Assert.Single(purchaseOrder.DomainEvents);
        var deleted = Assert.IsType<PurchaseOrderDeletedDomainEvent>(domainEvent);
        Assert.Equal(purchaseOrder.Id, deleted.PurchaseOrderId);
        Assert.Equal(TestClients.TenantId, deleted.TenantId);
    }

    [Fact]
    public async Task Creating_a_po_for_an_unknown_client_is_not_found()
    {
        var clients = new InMemoryClientRepository();
        var purchaseOrders = new InMemoryPurchaseOrderRepository();
        var handler = new CreatePurchaseOrderCommandHandler(clients, purchaseOrders);

        var command = new CreatePurchaseOrderCommand(
            TestClients.TenantId, Guid.NewGuid(), "PO-88231", new DateOnly(2026, 1, 15), null, null, null, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.NotFound, result.Error);
        Assert.Empty(purchaseOrders.PurchaseOrders);
    }

    [Fact]
    public async Task Creating_a_po_persists_it_and_returns_its_id()
    {
        var clients = new InMemoryClientRepository();
        var client = TestClients.Create();
        clients.Add(client);
        var purchaseOrders = new InMemoryPurchaseOrderRepository();
        var handler = new CreatePurchaseOrderCommandHandler(clients, purchaseOrders);

        var command = new CreatePurchaseOrderCommand(
            TestClients.TenantId, client.Id, "PO-88231", new DateOnly(2026, 1, 15), null, 250_000m, 305m, 180m, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var purchaseOrder = Assert.Single(purchaseOrders.PurchaseOrders);
        Assert.Equal(result.Value, purchaseOrder.Id);
        Assert.Equal(client.Id, purchaseOrder.ClientId);
        Assert.Equal(305m, purchaseOrder.RoundTripRateCad);
        Assert.Equal(180m, purchaseOrder.OneWayRateCad);
        Assert.Equal(1, purchaseOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deleting_a_po_raises_the_removal_event_before_the_row_goes()
    {
        var purchaseOrders = new InMemoryPurchaseOrderRepository();
        var purchaseOrder = Create().Value;
        purchaseOrders.Add(purchaseOrder);
        purchaseOrder.ClearDomainEvents();
        var handler = new DeletePurchaseOrderCommandHandler(purchaseOrders);

        var result = await handler.Handle(
            new DeletePurchaseOrderCommand(TestClients.TenantId, purchaseOrder.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(purchaseOrders.PurchaseOrders);
        Assert.Single(purchaseOrder.DomainEvents.OfType<PurchaseOrderDeletedDomainEvent>());
    }

    [Fact]
    public async Task Deleting_a_po_removes_it()
    {
        var purchaseOrders = new InMemoryPurchaseOrderRepository();
        var purchaseOrder = Create().Value;
        purchaseOrders.Add(purchaseOrder);
        var handler = new DeletePurchaseOrderCommandHandler(purchaseOrders);

        var result = await handler.Handle(
            new DeletePurchaseOrderCommand(TestClients.TenantId, purchaseOrder.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(purchaseOrders.PurchaseOrders);
    }

    [Fact]
    public async Task Deleting_an_unknown_po_is_not_found()
    {
        var handler = new DeletePurchaseOrderCommandHandler(new InMemoryPurchaseOrderRepository());

        var result = await handler.Handle(
            new DeletePurchaseOrderCommand(TestClients.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseOrderErrors.NotFound, result.Error);
    }
}
