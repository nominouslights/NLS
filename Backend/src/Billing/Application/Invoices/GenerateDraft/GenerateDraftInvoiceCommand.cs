using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GenerateDraft;

/// <summary>
/// Generates draft worksheets for one client's billing period from its completed, uninvoiced
/// round trips, priced from each purchase order's own terms with the contract rate as
/// fallback. Produces <b>one worksheet per effective purchase order</b>, so the result is a
/// list, not a single id.
/// </summary>
public sealed record GenerateDraftInvoiceCommand(
    Guid TenantId,
    Guid ClientId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd) : ICommand<GenerateDraftInvoicesResult>;

/// <summary>
/// One generated worksheet: its id, the invoice number it was assigned, the purchase order it
/// covers (null when neither the trips nor the contract named one), its line count and total,
/// and any non-blocking warnings (today: the PO's authorized value being exceeded).
/// </summary>
public sealed record GeneratedInvoiceDraft(
    Guid InvoiceId,
    string InvoiceNumber,
    string? PoNumber,
    int LineCount,
    decimal TotalCad,
    IReadOnlyList<string> Warnings);

/// <summary>
/// The result of one generation request: every worksheet it created, in deterministic order
/// (PO-number order, with the no-PO worksheet last). Always at least one entry — a client with
/// no completed trips still gets a single empty worksheet to add manual lines to.
/// </summary>
public sealed record GenerateDraftInvoicesResult(IReadOnlyList<GeneratedInvoiceDraft> Invoices);
