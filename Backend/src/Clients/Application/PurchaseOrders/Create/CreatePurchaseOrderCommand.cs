using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.PurchaseOrders.Create;

/// <summary>Records a purchase order a client issued. Returns the new PO's id.</summary>
public sealed record CreatePurchaseOrderCommand(
    Guid TenantId,
    Guid ClientId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    string? Note) : ICommand<Guid>;
