namespace NorthernLink.Clients.Application.PurchaseOrders;

/// <summary>The Clients module's public representation of a purchase order.</summary>
public sealed record PurchaseOrderResponse(
    Guid Id,
    Guid ClientId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
