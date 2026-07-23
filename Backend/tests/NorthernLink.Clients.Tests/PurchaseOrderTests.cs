using NorthernLink.Clients.Application.PurchaseOrders.Create;
using NorthernLink.Clients.Application.PurchaseOrders.Delete;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.PurchaseOrders;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>PurchaseOrder factory validation and create/delete handler behavior.</summary>
public class PurchaseOrderTests
{
    private static NorthernLink.Shared.Kernel.Result<PurchaseOrder> Create(
        string poNumber = "PO-88231",
        DateOnly? issued = null,
        DateOnly? expiry = null,
        decimal? amountCad = 250_000m) =>
        PurchaseOrder.Create(
            TestClients.TenantId,
            Guid.NewGuid(),
            poNumber,
            issued ?? new DateOnly(2026, 1, 15),
            expiry,
            amountCad,
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
            "PO-99000", new DateOnly(2026, 2, 1), new DateOnly(2026, 12, 31), 300_000m, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("PO-99000", purchaseOrder.PoNumber);
        Assert.Equal(300_000m, purchaseOrder.AmountCad);
        Assert.Null(purchaseOrder.Note);
    }

    [Fact]
    public async Task Creating_a_po_for_an_unknown_client_is_not_found()
    {
        var clients = new InMemoryClientRepository();
        var purchaseOrders = new InMemoryPurchaseOrderRepository();
        var handler = new CreatePurchaseOrderCommandHandler(clients, purchaseOrders);

        var command = new CreatePurchaseOrderCommand(
            TestClients.TenantId, Guid.NewGuid(), "PO-88231", new DateOnly(2026, 1, 15), null, null, null);

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
            TestClients.TenantId, client.Id, "PO-88231", new DateOnly(2026, 1, 15), null, 250_000m, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var purchaseOrder = Assert.Single(purchaseOrders.PurchaseOrders);
        Assert.Equal(result.Value, purchaseOrder.Id);
        Assert.Equal(client.Id, purchaseOrder.ClientId);
        Assert.Equal(1, purchaseOrders.SaveChangesCallCount);
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
