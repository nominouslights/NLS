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
/// Full worksheet detail, lines included. <c>NetTermsDays</c> is an informational snapshot.
/// <c>QboInvoiceId</c>/<c>QboEnteredDate</c> record the manual QBO reconciliation and
/// <c>PaymentConfirmedDate</c> the manually confirmed settlement (null while outstanding).
/// <c>OutstandingCad</c> is what the platform still expects to collect — the total while the
/// worksheet sits in QuickBooks unpaid, and zero once it is paid, voided, or written off. That
/// zeroing is what writing off a balance means here; there is no stored balance to adjust.
/// QuickBooks still owns sent/overdue and partial settlement — no aging here.
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
    DateOnly? PaymentConfirmedDate,
    decimal? WrittenOffAmountCad,
    DateOnly? WrittenOffDate,
    string? WrittenOffReason,
    decimal OutstandingCad,
    IReadOnlyList<InvoiceLineResponse> Lines);

/// <summary>
/// Worksheet list row (no lines), served from <c>rm_invoices</c>. Carries the QBO
/// reconciliation and payment-confirmation fields so the frontend can show entered-in-QBO
/// and outstanding/paid state without another call.
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
    DateOnly? QboEnteredDate,
    DateOnly? PaymentConfirmedDate,
    decimal? WrittenOffAmountCad,
    DateOnly? WrittenOffDate,
    string? WrittenOffReason,
    decimal OutstandingCad);
