using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class PurchaseOrderLineItem : Entity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal UnitRate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal LineTotal { get; private set; }
    public int SortOrder { get; private set; }

    private PurchaseOrderLineItem() { }

    public static PurchaseOrderLineItem Create(
        Guid purchaseOrderId,
        string description,
        decimal unitRate,
        decimal quantity,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        if (unitRate < 0)
            throw new ArgumentException("Unit rate cannot be negative.", nameof(unitRate));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        return new PurchaseOrderLineItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrderId,
            Description = description.Trim(),
            UnitRate = unitRate,
            Quantity = quantity,
            LineTotal = unitRate * quantity,
            SortOrder = sortOrder
        };
    }
}
