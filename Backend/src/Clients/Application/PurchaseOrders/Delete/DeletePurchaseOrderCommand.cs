using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.PurchaseOrders.Delete;

/// <summary>
/// Hard-deletes a purchase order (invoices only ever snapshot the PO number as a string,
/// so nothing dangles). The audit pipeline records a final snapshot plus a synthetic
/// aggregate-deleted journal row, which is what removes the read-model row.
/// </summary>
public sealed record DeletePurchaseOrderCommand(Guid TenantId, Guid PurchaseOrderId) : ICommand;
