using NorthernLink.Billing.Domain.PurchaseOrders;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>
/// The <c>purchase_order_snapshots</c> replica. <see cref="GetByIdAsync"/> takes an explicit
/// tenant id because its caller is the integration-event consumer, which runs outside any
/// HTTP request (no ambient tenant — the Fleet inspection-consumer pattern).
/// <see cref="GetForClientAsync"/> runs on the request path and stays behind the tenant
/// filter.
/// <para>
/// There is deliberately no by-PO-number lookup: draft generation resolves every PO it needs
/// from one <see cref="GetForClientAsync"/> round trip and matches numbers in memory
/// (case-insensitively — a trip's PO number and the PO's own are typed by hand in two
/// different screens), rather than issuing one query per PO.
/// </para>
/// </summary>
public interface IPurchaseOrderSnapshotRepository
{
    Task<PurchaseOrderSnapshot?> GetByIdAsync(
        Guid tenantId, Guid purchaseOrderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseOrderSnapshot>> GetForClientAsync(
        Guid clientId, CancellationToken cancellationToken = default);

    void Add(PurchaseOrderSnapshot snapshot);

    void Remove(PurchaseOrderSnapshot snapshot);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
