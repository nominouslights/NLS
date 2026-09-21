using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.PurchaseOrders.Update;

/// <summary>
/// Updates an existing purchase order's details, including its own pricing terms. A full
/// replace: a null <see cref="RoundTripRateCad"/> / <see cref="OneWayRateCad"/> clears that
/// term rather than leaving the previous value, returning the PO to the contract fallback.
/// </summary>
public sealed record UpdatePurchaseOrderCommand(
    Guid TenantId,
    Guid PurchaseOrderId,
    string PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    decimal? RoundTripRateCad,
    decimal? OneWayRateCad,
    string? Note) : ICommand;
