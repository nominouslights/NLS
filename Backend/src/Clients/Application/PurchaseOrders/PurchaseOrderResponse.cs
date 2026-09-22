namespace NorthernLink.Clients.Application.PurchaseOrders;

/// <summary>
/// The Clients module's public representation of a purchase order, including its own
/// pricing terms. <see cref="RoundTripRateCad"/> / <see cref="OneWayRateCad"/> are null when
/// this PO sets no term of that kind — the contract rate applies instead. Both are
/// tax-inclusive; the platform computes no tax.
/// </summary>
public sealed record PurchaseOrderResponse(
    Guid Id,
    Guid ClientId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    decimal? RoundTripRateCad,
    decimal? OneWayRateCad,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
