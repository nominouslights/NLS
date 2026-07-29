namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// Billing-worksheet lifecycle. Draft is the only editable state; EnteredInQbo records that
/// the numbers have been keyed into QuickBooks Online by hand (QBO now owns
/// sent/paid/overdue/receivables — the platform never calls the QBO API). Void is terminal
/// and only reachable from Draft, releasing the draft's claimed trips.
/// </summary>
public enum InvoiceStatus
{
    Draft,
    EnteredInQbo,
    Void,
}
