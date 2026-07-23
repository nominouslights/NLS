using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.PurchaseOrders;

/// <summary>
/// A purchase order a client issued against their account, referenced when invoicing
/// (invoices snapshot the PO number as a string, so hard-deleting a PO never dangles).
/// Client-scoped reference data with no lifecycle of its own: create/update/hard-delete,
/// no domain events — the audit pipeline's snapshots and the synthetic aggregate-deleted
/// journal row are the record.
/// </summary>
public sealed class PurchaseOrder : AggregateRoot, ITenantScoped
{
    private PurchaseOrder()
    {
        // EF Core materialization only.
        PoNumber = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid ClientId { get; private set; }
    public string PoNumber { get; private set; }
    public DateOnly Issued { get; private set; }
    public DateOnly? Expiry { get; private set; }
    public decimal? AmountCad { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<PurchaseOrder> Create(
        Guid tenantId,
        Guid clientId,
        string poNumber,
        DateOnly issued,
        DateOnly? expiry,
        decimal? amountCad,
        string? note)
    {
        if (Validate(poNumber, issued, expiry, amountCad) is { } error)
        {
            return Result.Failure<PurchaseOrder>(error);
        }

        var now = DateTimeOffset.UtcNow;
        var purchaseOrder = new PurchaseOrder
        {
            TenantId = tenantId,
            ClientId = clientId,
            PoNumber = poNumber.Trim(),
            Issued = issued,
            Expiry = expiry,
            AmountCad = amountCad,
            Note = Clean(note),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        return Result.Success(purchaseOrder);
    }

    public Result Update(
        string poNumber,
        DateOnly issued,
        DateOnly? expiry,
        decimal? amountCad,
        string? note)
    {
        if (Validate(poNumber, issued, expiry, amountCad) is { } error)
        {
            return Result.Failure(error);
        }

        PoNumber = poNumber.Trim();
        Issued = issued;
        Expiry = expiry;
        AmountCad = amountCad;
        Note = Clean(note);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    private static Error? Validate(string poNumber, DateOnly issued, DateOnly? expiry, decimal? amountCad)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return PurchaseOrderErrors.PoNumberRequired;
        }

        if (expiry is { } expiryDate && expiryDate < issued)
        {
            return PurchaseOrderErrors.InvalidExpiry;
        }

        if (amountCad is { } amount && amount <= 0)
        {
            return PurchaseOrderErrors.InvalidAmount;
        }

        return null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
