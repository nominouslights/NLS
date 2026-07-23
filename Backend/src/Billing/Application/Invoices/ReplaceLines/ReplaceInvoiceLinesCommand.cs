using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.ReplaceLines;

/// <summary>
/// One incoming line for a draft edit. Amounts are never accepted from the client — the
/// domain computes quantity × unit price. A null <see cref="LineId"/> means a new line;
/// <see cref="TripIds"/> (possibly empty for manual lines) drives claim reconciliation.
/// </summary>
public sealed record InvoiceLineInput(
    Guid? LineId,
    string Description,
    IReadOnlyList<Guid>? TripIds,
    string? TripNumber,
    DateOnly? ServiceDate,
    decimal Quantity,
    decimal UnitPriceCad);

/// <summary>Replaces a draft invoice's whole line list (the only line-edit operation).</summary>
public sealed record ReplaceInvoiceLinesCommand(
    Guid TenantId,
    Guid InvoiceId,
    IReadOnlyList<InvoiceLineInput> Lines) : ICommand;
