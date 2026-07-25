using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.PurchaseOrders.Update;

/// <summary>Updates an existing purchase order's details.</summary>
public sealed record UpdatePurchaseOrderCommand(
    Guid TenantId,
    Guid PurchaseOrderId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    string? Note) : ICommand;
