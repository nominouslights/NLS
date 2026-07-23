using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// One invoice line, persisted as jsonb on the invoice row (no child table — lines are
/// replaced wholesale while the invoice is a draft, never addressed individually by SQL).
/// <see cref="TripIds"/> ties the line back to the <c>billable_trips</c> replica rows it
/// prices, so line edits can reconcile trip claims; a manual line simply has none.
/// <see cref="AmountCad"/> is always computed here from quantity × unit price — callers
/// never supply it, so a line can never disagree with its own arithmetic.
/// </summary>
public sealed record InvoiceLine
{
    public required Guid LineId { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<Guid> TripIds { get; init; }
    public string? TripNumber { get; init; }
    public DateOnly? ServiceDate { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPriceCad { get; init; }
    public required decimal AmountCad { get; init; }

    public static Result<InvoiceLine> Create(
        string description,
        IReadOnlyList<Guid>? tripIds,
        string? tripNumber,
        DateOnly? serviceDate,
        decimal quantity,
        decimal unitPriceCad,
        Guid? lineId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<InvoiceLine>(InvoiceErrors.InvalidLineDescription);
        }

        if (quantity <= 0)
        {
            return Result.Failure<InvoiceLine>(InvoiceErrors.InvalidLineQuantity);
        }

        if (unitPriceCad < 0)
        {
            return Result.Failure<InvoiceLine>(InvoiceErrors.InvalidLineUnitPrice);
        }

        return Result.Success(new InvoiceLine
        {
            LineId = lineId ?? Guid.NewGuid(),
            Description = description.Trim(),
            TripIds = tripIds?.ToList() ?? [],
            TripNumber = string.IsNullOrWhiteSpace(tripNumber) ? null : tripNumber.Trim(),
            ServiceDate = serviceDate,
            Quantity = quantity,
            UnitPriceCad = unitPriceCad,
            AmountCad = Math.Round(quantity * unitPriceCad, 2),
        });
    }
}
