namespace NorthernLink.Billing.Application.Invoices;

/// <summary>
/// One invoice line as the API exposes it. <c>AmountCad</c> is server-computed
/// (quantity × unit price) — clients send description/quantity/price, never amounts.
/// </summary>
public sealed record InvoiceLineResponse(
    Guid LineId,
    string Description,
    IReadOnlyList<Guid> TripIds,
    string? TripNumber,
    DateOnly? ServiceDate,
    decimal Quantity,
    decimal UnitPriceCad,
    decimal AmountCad);

/// <summary>
/// Full worksheet detail, lines included. <c>NetTermsDays</c> is an informational snapshot;
/// receivables (sent/paid/overdue) live in QuickBooks, not here. <c>QboInvoiceId</c> and
/// <c>QboEnteredDate</c> record the manual QBO reconciliation.
/// </summary>
public sealed record InvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    Guid ClientId,
    string ClientName,
    Guid? ContractId,
    string? PoNumber,
    string? BudgetCode,
    int NetTermsDays,
    bool GstApplicable,
    decimal GstRate,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    DateTimeOffset IssuedAtUtc,
    decimal SubtotalCad,
    decimal GstCad,
    decimal TotalCad,
    string? QboInvoiceId,
    DateOnly? QboEnteredDate,
    IReadOnlyList<InvoiceLineResponse> Lines);

/// <summary>
/// Worksheet list row (no lines), served from <c>rm_invoices</c>. Carries the QBO
/// reconciliation fields so the frontend can show entered-in-QBO state without another call.
/// </summary>
public sealed record InvoiceSummaryResponse(
    Guid Id,
    string InvoiceNumber,
    Guid ClientId,
    string ClientName,
    string? PoNumber,
    string? BudgetCode,
    int NetTermsDays,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    DateTimeOffset IssuedAtUtc,
    decimal SubtotalCad,
    decimal GstCad,
    decimal TotalCad,
    int LineCount,
    string? QboInvoiceId,
    DateOnly? QboEnteredDate);
