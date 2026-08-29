namespace NorthernLink.Notifications.Application.Dispatches;

/// <summary>
/// The data for one client accruals-report PDF — a flat, presentation-ready snapshot of a
/// client's month bucketed by billing state, composed entirely by the dispatcher's screen.
/// All fields are already-formatted strings (labels, dates, dollar amounts with any "est."
/// markings baked in); the PDF renderer does no domain lookups of its own — Notifications
/// holds no trips/billing/clients data by design, same as <see cref="PickupEmailReport"/>.
/// </summary>
public sealed record ClientAccrualsReport(
    string ClientName,
    string PeriodLabel,
    string PreparedDate,
    IReadOnlyList<string> Notes,
    IReadOnlyList<AccrualsSummaryRow> Summary,
    IReadOnlyList<AccrualsReportBucket> Buckets,
    IReadOnlyList<AccrualsReconciliationRow> Reconciliation,
    IReadOnlyList<AccrualsInvoiceRow> Invoices);

/// <summary>One bucket's line in the summary table: label, round-trip count, and totals.</summary>
public sealed record AccrualsSummaryRow(
    string BucketLabel,
    string RoundTrips,
    string ActualCad,
    string EstimatedCad);

/// <summary>One billing-state bucket's detail section: its label and per-group rows.</summary>
public sealed record AccrualsReportBucket(
    string Label,
    IReadOnlyList<AccrualsGroupRow> Rows);

/// <summary>
/// One round-trip group's line in a bucket table. <paramref name="Reference"/> is the invoice
/// number, a worksheet reference, or "—"; <paramref name="AmountCad"/> carries any "est."
/// suffix or "amount unavailable" text pre-formatted.
/// </summary>
public sealed record AccrualsGroupRow(
    string Date,
    string TripNumbers,
    string Route,
    string PoNumber,
    string Reference,
    string AmountCad);

/// <summary>One cancelled/written-off group's line in the reconciliation section.</summary>
public sealed record AccrualsReconciliationRow(
    string Date,
    string TripNumbers,
    string Route,
    string Status,
    string Reason,
    string AmountCad);

/// <summary>One referenced invoice's line — the only place GST appears in the report.</summary>
public sealed record AccrualsInvoiceRow(
    string InvoiceNumber,
    string Status,
    string SubtotalCad,
    string GstCad,
    string TotalCad);
