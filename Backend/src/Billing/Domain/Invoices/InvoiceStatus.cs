namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// Invoice lifecycle. Draft is the only editable state; Void is terminal and only
/// reachable from Draft (a sent invoice is corrected with a credit/QBO-side action,
/// never voided here). "Overdue" is deliberately NOT a status — it is derived on the
/// read side as <c>Sent &amp;&amp; today &gt; SentAt + NetTermsDays</c>.
/// </summary>
public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    Void,
}
