namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// QuickBooks Online reconciliation state, set by hand (or a future import job) — the
/// platform never calls the QBO API. QBO is a read-only book of record: the invoice is
/// authored here, exported/entered there, and this flag records whether the two agree.
/// </summary>
public enum QboSyncStatus
{
    NotSynced,
    Matched,
    UnmatchedPayment,
}
