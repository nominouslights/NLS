namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// Billing-worksheet lifecycle: Draft → EnteredInQbo → Paid, with Void terminal off Draft and
/// WrittenOff terminal off EnteredInQbo.
/// <para>
/// Draft is the only editable state. EnteredInQbo records that the numbers have been keyed into
/// QuickBooks Online by hand. Paid records that payment against that QBO invoice has been
/// confirmed — also entered by hand, so the platform can answer "outstanding vs paid" without
/// calling the QBO API (it never does; QBO remains the accounting system of record and still
/// owns sent/overdue and any partial-settlement detail).
/// </para>
/// <para>
/// Entry into QuickBooks is a one-way door. There is no reopen: once a worksheet has been keyed
/// in, the invoice it represents exists in QBO and can be corrected or written off, never
/// un-sent. Payment confirmation is the one reversible step (Paid → EnteredInQbo, confirmed in
/// error). Void is only reachable from Draft and releases that draft's claimed trips;
/// <see cref="WrittenOff"/> deliberately does not — a written-off trip must never drift back
/// into the billable pool and get invoiced a second time.
/// </para>
/// <para>
/// Ordinals are explicit so a stored value can never be reinterpreted by a future reordering.
/// </para>
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    EnteredInQbo = 1,
    Void = 2,
    Paid = 3,

    /// <summary>Entered in QuickBooks but never collected — the balance was written off.</summary>
    WrittenOff = 4,
}
