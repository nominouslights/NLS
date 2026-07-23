using NorthernLink.Clients.Domain.PurchaseOrders;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="PurchaseOrder"/> into <c>clients.rm_purchase_orders</c>. Hard
/// deletes propagate through the base's source-gone path, driven by the synthetic
/// aggregate-deleted journal row the audit pipeline writes.
/// </summary>
internal sealed class PurchaseOrderProjection : ClientsProjection<PurchaseOrder, PurchaseOrderReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(PurchaseOrder));

    protected override void Map(PurchaseOrder source, PurchaseOrderReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.ClientId = source.ClientId;
        row.PoNumber = source.PoNumber;
        row.Issued = source.Issued;
        row.Expiry = source.Expiry;
        row.AmountCad = source.AmountCad;
        row.Note = source.Note;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
