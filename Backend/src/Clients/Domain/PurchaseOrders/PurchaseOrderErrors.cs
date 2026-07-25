using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.PurchaseOrders;

/// <summary>All domain errors the PurchaseOrder aggregate (and its handlers) can produce.</summary>
public static class PurchaseOrderErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Clients.PurchaseOrder.NotFound", "The purchase order was not found.");

    public static readonly Error PoNumberRequired = Error.Validation(
        "Clients.PurchaseOrder.PoNumberRequired", "A PO number is required.");

    public static readonly Error InvalidExpiry = Error.Validation(
        "Clients.PurchaseOrder.InvalidExpiry", "The PO expiry date cannot be before its issue date.");

    public static readonly Error InvalidAmount = Error.Validation(
        "Clients.PurchaseOrder.InvalidAmount", "The PO amount must be greater than zero.");
}
