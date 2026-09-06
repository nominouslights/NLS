using NorthernLink.Billing.Domain.Invoices.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// A billing worksheet — the platform prepares the numbers (completed uninvoiced round trips
/// priced at the contract rate, plus manual lines) that are then keyed into QuickBooks Online
/// by hand. <b>The platform never computes or applies GST/HST/PST or any other tax</b>:
/// QuickBooks Online owns all tax calculation, so a worksheet carries one money figure —
/// <see cref="TotalCad"/>, the plain sum of its lines — and no tax fields at all. The platform
/// never calls the QBO API either — QBO remains the accounting system of record and owns
/// sent/overdue and any partial-settlement detail — but two facts are recorded here by hand so
/// dispatch can answer them without opening QBO: that the worksheet was entered
/// (<see cref="QboInvoiceId"/>, <see cref="QboEnteredDate"/>) and that payment was confirmed
/// (<see cref="PaymentConfirmedDate"/>). Everything contract-derived
/// (<see cref="PoNumber"/>, <see cref="BudgetCode"/>, <see cref="NetTermsDays"/>) is a snapshot
/// taken at drafting: later contract amendments never rewrite an existing worksheet. The total
/// is computed, never stored on the write side — a line list can't disagree with its own total.
/// The QBO fields record the manual reconciliation: <see cref="QboInvoiceId"/> is the QBO
/// invoice number and <see cref="QboEnteredDate"/> the date it was keyed in.
/// </summary>
public sealed class Invoice : AggregateRoot, ITenantScoped
{
    private readonly List<InvoiceLine> _lines = [];

    private Invoice()
    {
        // EF Core materialization only.
        InvoiceNumber = null!;
        ClientName = null!;
    }

    public Guid TenantId { get; private set; }
    public string InvoiceNumber { get; private set; }
    public Guid ClientId { get; private set; }
    public string ClientName { get; private set; }
    public Guid? ContractId { get; private set; }
    public string? PoNumber { get; private set; }
    public string? BudgetCode { get; private set; }

    /// <summary>Informational snapshot of the contract's net terms at drafting — the platform
    /// no longer derives due dates or overdue state; QBO owns receivables.</summary>
    public int NetTermsDays { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }

    /// <summary>The QBO invoice number this worksheet was entered as (manual reconciliation).</summary>
    public string? QboInvoiceId { get; private set; }

    /// <summary>The date the worksheet was keyed into QBO. Null until entered.</summary>
    public DateOnly? QboEnteredDate { get; private set; }

    /// <summary>
    /// The date payment against the QBO invoice was confirmed, entered by hand. Null while
    /// outstanding — "outstanding vs paid" is exactly this field being null or not.
    /// </summary>
    public DateOnly? PaymentConfirmedDate { get; private set; }

    /// <summary>How much was written off. Null unless <see cref="Status"/> is WrittenOff.</summary>
    public decimal? WrittenOffAmountCad { get; private set; }

    public DateOnly? WrittenOffDate { get; private set; }

    /// <summary>Why it was written off — required, and the whole point of recording the write-off.</summary>
    public string? WrittenOffReason { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    /// <summary>
    /// The worksheet's one money figure: the plain sum of its lines, with no tax uplift. There
    /// is deliberately no subtotal/total split — the platform applies no tax, so a second figure
    /// could only ever drift from this one.
    /// </summary>
    public decimal TotalCad => Math.Round(_lines.Sum(line => line.AmountCad), 2);

    /// <summary>
    /// What the platform still expects to collect. Computed, not stored: only an invoice sitting
    /// in QuickBooks unpaid is outstanding — a draft was never sent, a void never existed, a paid
    /// one settled, and a written-off one is the balance being zeroed. That zeroing is exactly
    /// what "write off the balance" means here; there is no separate balance column to adjust.
    /// </summary>
    public decimal OutstandingCad => Status == InvoiceStatus.EnteredInQbo ? TotalCad : 0m;

    public static Result<Invoice> CreateDraft(
        Guid tenantId,
        string invoiceNumber,
        Guid clientId,
        string clientName,
        Guid? contractId,
        string? poNumber,
        string? budgetCode,
        int netTermsDays,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<InvoiceLine> lines)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return Result.Failure<Invoice>(InvoiceErrors.InvalidInvoiceNumber);
        }

        if (string.IsNullOrWhiteSpace(clientName))
        {
            return Result.Failure<Invoice>(InvoiceErrors.InvalidClientName);
        }

        if (periodEnd < periodStart)
        {
            return Result.Failure<Invoice>(InvoiceErrors.InvalidPeriod);
        }

        if (netTermsDays < 0)
        {
            return Result.Failure<Invoice>(InvoiceErrors.InvalidNetTerms);
        }

        var invoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber.Trim(),
            ClientId = clientId,
            ClientName = clientName.Trim(),
            ContractId = contractId,
            PoNumber = string.IsNullOrWhiteSpace(poNumber) ? null : poNumber.Trim(),
            BudgetCode = string.IsNullOrWhiteSpace(budgetCode) ? null : budgetCode.Trim(),
            NetTermsDays = netTermsDays,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = InvoiceStatus.Draft,
            IssuedAtUtc = DateTimeOffset.UtcNow,
        };

        invoice._lines.AddRange(lines);
        invoice.Raise(new InvoiceDraftedDomainEvent(invoice.Id, tenantId, invoice.InvoiceNumber, clientId));
        return Result.Success(invoice);
    }

    /// <summary>
    /// Replaces the whole line list (draft only) — the "slight edits on the draft" rule.
    /// The caller (handler) reconciles billable-trip claims against the new
    /// <see cref="InvoiceLine.TripIds"/> before saving.
    /// </summary>
    public Result ReplaceLines(IReadOnlyList<InvoiceLine> lines)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return Result.Failure(InvoiceErrors.NotDraft);
        }

        _lines.Clear();
        _lines.AddRange(lines);

        Raise(new InvoiceLinesReplacedDomainEvent(Id, _lines.Count, TotalCad));
        return Result.Success();
    }

    /// <summary>
    /// Records that the worksheet has been keyed into QuickBooks Online (Draft→EnteredInQbo).
    /// Lines lock and the claimed trips stay claimed permanently. No QBO API call — this only
    /// stamps the manually-entered reference.
    /// </summary>
    public Result MarkEnteredInQbo(string qboInvoiceId, DateOnly enteredDate)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return Result.Failure(InvoiceErrors.AlreadyEntered);
        }

        if (string.IsNullOrWhiteSpace(qboInvoiceId))
        {
            return Result.Failure(InvoiceErrors.QboInvoiceIdRequired);
        }

        Status = InvoiceStatus.EnteredInQbo;
        QboInvoiceId = qboInvoiceId.Trim();
        QboEnteredDate = enteredDate;

        Raise(new InvoiceEnteredInQboDomainEvent(Id, QboInvoiceId, enteredDate));
        return Result.Success();
    }

    /// <summary>
    /// Corrects the recorded QBO reference on an already-entered worksheet. Allowed while Paid
    /// too: fixing a mistyped QBO number is a correction, not a lifecycle change, and forcing
    /// the payment confirmation to be cleared first would lose that fact to fix a typo.
    /// </summary>
    public Result UpdateQboReference(string qboInvoiceId, DateOnly enteredDate)
    {
        if (Status is not (InvoiceStatus.EnteredInQbo or InvoiceStatus.Paid))
        {
            return Result.Failure(InvoiceErrors.NotEntered);
        }

        if (string.IsNullOrWhiteSpace(qboInvoiceId))
        {
            return Result.Failure(InvoiceErrors.QboInvoiceIdRequired);
        }

        QboInvoiceId = qboInvoiceId.Trim();
        QboEnteredDate = enteredDate;

        Raise(new InvoiceEnteredInQboDomainEvent(Id, QboInvoiceId, enteredDate));
        return Result.Success();
    }

    /// <summary>
    /// Confirms that payment against the QBO invoice has been received (EnteredInQbo→Paid).
    /// Entered by hand like the QBO reference itself — no QBO API call. QuickBooks stays the
    /// accounting system of record; this only lets the platform answer "outstanding or paid".
    /// </summary>
    public Result ConfirmPayment(DateOnly confirmedDate)
    {
        if (Status != InvoiceStatus.EnteredInQbo)
        {
            return Result.Failure(InvoiceErrors.NotEnteredForPayment);
        }

        Status = InvoiceStatus.Paid;
        PaymentConfirmedDate = confirmedDate;

        Raise(new InvoicePaymentConfirmedDomainEvent(Id, confirmedDate));
        return Result.Success();
    }

    /// <summary>
    /// Clears a payment confirmation recorded in error (Paid→EnteredInQbo). The QBO reference
    /// is untouched — the worksheet is still entered, just no longer settled.
    /// </summary>
    public Result ClearPaymentConfirmation()
    {
        if (Status != InvoiceStatus.Paid)
        {
            return Result.Failure(InvoiceErrors.NotPaid);
        }

        Status = InvoiceStatus.EnteredInQbo;
        PaymentConfirmedDate = null;

        Raise(new InvoicePaymentConfirmedDomainEvent(Id, null));
        return Result.Success();
    }

    /// <summary>
    /// Writes off an outstanding worksheet: the client will not pay, and the balance goes to zero
    /// so the receivable stops counting. Only from EnteredInQbo — a draft was never sent (void it
    /// instead) and a paid one has nothing to write off.
    /// <para>
    /// The QBO reference is left intact: the invoice still exists in QuickBooks, and this only
    /// records that it will never be collected. The claimed trips are <em>not</em> released
    /// either — that is the difference from a void. Those runs happened and were billed once;
    /// putting them back in the billable pool would invite a second invoice for the same work.
    /// </para>
    /// </summary>
    public Result WriteOff(decimal amountCad, DateOnly writtenOffDate, string reason)
    {
        if (Status != InvoiceStatus.EnteredInQbo)
        {
            return Result.Failure(InvoiceErrors.NotEnteredForWriteOff);
        }

        if (amountCad <= 0m)
        {
            return Result.Failure(InvoiceErrors.InvalidWriteOffAmount);
        }

        if (amountCad > TotalCad)
        {
            return Result.Failure(InvoiceErrors.WriteOffExceedsTotal);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(InvoiceErrors.WriteOffReasonRequired);
        }

        Status = InvoiceStatus.WrittenOff;
        WrittenOffAmountCad = Math.Round(amountCad, 2);
        WrittenOffDate = writtenOffDate;
        WrittenOffReason = reason.Trim();

        Raise(new InvoiceWrittenOffDomainEvent(
            Id, WrittenOffAmountCad.Value, writtenOffDate, WrittenOffReason));
        return Result.Success();
    }

    /// <summary>Voids a draft. The handler releases the draft's billable-trip claims alongside.</summary>
    public Result Void()
    {
        if (Status != InvoiceStatus.Draft)
        {
            return Result.Failure(InvoiceErrors.NotDraft);
        }

        Status = InvoiceStatus.Void;

        Raise(new InvoiceStatusChangedDomainEvent(Id, InvoiceStatus.Draft, InvoiceStatus.Void));
        return Result.Success();
    }
}
