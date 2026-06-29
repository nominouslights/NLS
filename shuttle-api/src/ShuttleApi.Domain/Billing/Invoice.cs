using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Billing;

public sealed class Invoice : AggregateRoot<Guid>
{
    private readonly List<InvoiceLineItem> _lineItems = [];

    private Invoice() { }

    public Guid ClientId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public InvoiceStatus Status { get; private set; }
    public DateTime IssuedDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();
    public decimal SubTotal => _lineItems.Sum(x => x.LineTotal);
    public decimal TotalAmount => SubTotal;

    public static Invoice Create(
        Guid clientId,
        string invoiceNumber,
        DateTime issuedDate,
        DateTime dueDate,
        string? notes)
    {
        Guard.Against(clientId == Guid.Empty, "ClientId cannot be empty.");
        Guard.AgainstNullOrEmpty(invoiceNumber, nameof(invoiceNumber));

        return new Invoice
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            InvoiceNumber = invoiceNumber,
            Status = InvoiceStatus.Draft,
            IssuedDate = issuedDate,
            DueDate = dueDate,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddLineItem(InvoiceLineItem item) => _lineItems.Add(item);

    public void UpdateDraft(DateTime dueDate, string? notes)
    {
        Guard.Against(Status != InvoiceStatus.Draft, "Only draft invoices can be edited.");
        DueDate = dueDate;
        Notes = notes;
    }

    public Result MarkSent()
    {
        if (Status != InvoiceStatus.Draft) return Result.Failure("Only draft invoices can be sent.");
        Status = InvoiceStatus.Sent;
        return Result.Success();
    }

    public Result MarkPaid()
    {
        if (Status is not (InvoiceStatus.Sent or InvoiceStatus.Overdue))
            return Result.Failure("Only sent or overdue invoices can be marked paid.");
        Status = InvoiceStatus.Paid;
        PaidAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Void()
    {
        if (Status == InvoiceStatus.Paid) return Result.Failure("Paid invoices cannot be voided.");
        Status = InvoiceStatus.Void;
        return Result.Success();
    }
}
