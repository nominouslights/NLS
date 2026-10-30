using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.PurchaseOrders.Create;

/// <summary>
/// Records a purchase order a client issued. Returns the new PO's id.
/// <see cref="RoundTripRateCad"/> / <see cref="OneWayRateCad"/> are this PO's own negotiated
/// terms (tax-inclusive, both optional): whatever is left null falls back to the contract
/// rate at pricing time.
/// </summary>
public sealed record CreatePurchaseOrderCommand(
    Guid TenantId,
    Guid ClientId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    decimal? RoundTripRateCad,
    decimal? OneWayRateCad,
    string? Note) : ICommand<Guid>;
