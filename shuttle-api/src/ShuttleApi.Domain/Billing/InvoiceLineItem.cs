using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Billing;

public sealed class InvoiceLineItem : Entity<Guid>
{
    private InvoiceLineItem() { }

    public Guid InvoiceId { get; private set; }
    public Guid? TripId { get; private set; }
    public InvoiceLineType LineType { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal UnitRate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal LineTotal { get; private set; }
    public int SortOrder { get; private set; }

    public static InvoiceLineItem Create(
        Guid invoiceId,
        Guid? tripId,
        InvoiceLineType lineType,
        string description,
        decimal unitRate,
        decimal quantity,
        int sortOrder)
    {
        Guard.AgainstNullOrEmpty(description, nameof(description));

        return new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            TripId = tripId,
            LineType = lineType,
            Description = description,
            UnitRate = unitRate,
            Quantity = quantity,
            LineTotal = unitRate * quantity,
            SortOrder = sortOrder
        };
    }

    public void Update(string description, decimal unitRate, decimal quantity)
    {
        Guard.AgainstNullOrEmpty(description, nameof(description));
        Description = description;
        UnitRate = unitRate;
        Quantity = quantity;
        LineTotal = unitRate * quantity;
    }
}
