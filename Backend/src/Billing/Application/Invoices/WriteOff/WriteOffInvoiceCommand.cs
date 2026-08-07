using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.WriteOff;

/// <summary>
/// Writes off an outstanding worksheet — the client will not pay, so the balance goes to zero and
/// stops counting as a receivable. Recorded by hand like every other QBO-facing fact here; no API
/// call. The reason is required, and every trip the invoice claims moves to its own terminal
/// WrittenOff without being released back into the billable pool.
/// </summary>
public sealed record WriteOffInvoiceCommand(
    Guid TenantId,
    Guid InvoiceId,
    decimal AmountCad,
    DateOnly WrittenOffDate,
    string Reason) : ICommand;
