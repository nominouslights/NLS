using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class PurchaseOrder : Entity<Guid>
{
    private readonly List<PurchaseOrderLineItem> _lineItems = [];

    public Guid ClientId { get; private set; }
    public string PoNumber { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public string? Details { get; private set; }
    public decimal TotalValue { get; private set; }

    public IReadOnlyList<PurchaseOrderLineItem> LineItems => _lineItems.AsReadOnly();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        Guid clientId,
        string poNumber,
        DateTime startDate,
        string? details,
        IEnumerable<(string Description, decimal UnitRate, decimal Quantity)> lineItems)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
            throw new ArgumentException("PO number is required.", nameof(poNumber));

        var items = lineItems.ToList();
        if (items.Count == 0)
            throw new ArgumentException("At least one line item is required.");

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            PoNumber = poNumber.Trim(),
            StartDate = startDate,
            Details = details
        };

        purchaseOrder.ReplaceLineItems(items);
        return purchaseOrder;
    }

    public void Update(
        string poNumber,
        DateTime startDate,
        string? details,
        IEnumerable<(string Description, decimal UnitRate, decimal Quantity)> lineItems)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
            throw new ArgumentException("PO number is required.", nameof(poNumber));

        PoNumber = poNumber.Trim();
        StartDate = startDate;
        Details = details;
        ReplaceLineItems(lineItems);
    }

    private void ReplaceLineItems(IEnumerable<(string Description, decimal UnitRate, decimal Quantity)> lineItems)
    {
        var items = lineItems.ToList();
        if (items.Count == 0)
            throw new ArgumentException("At least one line item is required.");

        _lineItems.Clear();

        var sortOrder = 0;
        foreach (var (description, unitRate, quantity) in items)
        {
            _lineItems.Add(PurchaseOrderLineItem.Create(Id, description, unitRate, quantity, sortOrder++));
        }

        TotalValue = _lineItems.Sum(i => i.LineTotal);
    }
}
