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
/// Full invoice detail, lines included. <c>IsOverdue</c>/<c>DueDate</c> are derived at
/// read time from SentAtUtc + NetTermsDays — overdue is never a stored status.
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
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateOnly? DueDate,
    bool IsOverdue,
    decimal SubtotalCad,
    decimal GstCad,
    decimal TotalCad,
    string? QboInvoiceId,
    string QboSyncStatus,
    IReadOnlyList<InvoiceLineResponse> Lines);

/// <summary>
/// Invoice list row (no lines), served from <c>rm_invoices</c>. Carries SentAtUtc and
/// NetTermsDays alongside the derived DueDate/IsOverdue so the frontend can also compute
/// overdue/AR age client-side without another call.
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
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateOnly? DueDate,
    bool IsOverdue,
    decimal SubtotalCad,
    decimal GstCad,
    decimal TotalCad,
    int LineCount,
    string? QboInvoiceId,
    string QboSyncStatus);
