using NorthernLink.Billing.Domain.Invoices.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// A client invoice — the platform's authored record of what a billing period costs, built
/// from completed billable trips at the contract rate (plus manual lines). Everything
/// contract-derived (<see cref="PoNumber"/>, <see cref="BudgetCode"/>,
/// <see cref="NetTermsDays"/>, <see cref="GstApplicable"/>, <see cref="GstRate"/>) is a
/// snapshot taken at drafting: later contract amendments never rewrite an issued invoice.
/// Totals are computed, never stored on the write side — a line list can't disagree with
/// its own subtotal. "Overdue" is deliberately derived downstream
/// (<c>Sent &amp;&amp; today &gt; SentAt + NetTermsDays</c>), never persisted. The QBO
/// fields record manual reconciliation against QuickBooks Online; there are no QBO API
/// calls anywhere.
/// </summary>
public sealed class Invoice : AggregateRoot, ITenantScoped
{
    /// <summary>GST rate snapshotted onto new drafts (5%). Stored per invoice so a future
    /// rate change never silently reprices history.</summary>
    public const decimal StandardGstRate = 0.05m;

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
    public int NetTermsDays { get; private set; }
    public bool GstApplicable { get; private set; }
    public decimal GstRate { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public string? QboInvoiceId { get; private set; }
    public QboSyncStatus QboSyncStatus { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public decimal SubtotalCad => Math.Round(_lines.Sum(line => line.AmountCad), 2);

    public decimal GstCad => GstApplicable ? Math.Round(SubtotalCad * GstRate, 2) : 0m;

    public decimal TotalCad => SubtotalCad + GstCad;

    public static Result<Invoice> CreateDraft(
        Guid tenantId,
        string invoiceNumber,
        Guid clientId,
        string clientName,
        Guid? contractId,
        string? poNumber,
        string? budgetCode,
        int netTermsDays,
        bool gstApplicable,
        decimal gstRate,
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
            GstApplicable = gstApplicable,
            GstRate = gstRate,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = InvoiceStatus.Draft,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            QboSyncStatus = QboSyncStatus.NotSynced,
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

        Raise(new InvoiceLinesReplacedDomainEvent(Id, _lines.Count, SubtotalCad, TotalCad));
        return Result.Success();
    }

    public Result Send()
    {
        if (Status != InvoiceStatus.Draft)
        {
            return Result.Failure(InvoiceErrors.AlreadySent);
        }

        Status = InvoiceStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;

        Raise(new InvoiceStatusChangedDomainEvent(Id, InvoiceStatus.Draft, InvoiceStatus.Sent));
        return Result.Success();
    }

    public Result MarkPaid()
    {
        if (Status != InvoiceStatus.Sent)
        {
            return Result.Failure(InvoiceErrors.NotSent);
        }

        Status = InvoiceStatus.Paid;
        PaidAtUtc = DateTimeOffset.UtcNow;

        Raise(new InvoiceStatusChangedDomainEvent(Id, InvoiceStatus.Sent, InvoiceStatus.Paid));
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

    /// <summary>Records the manual QBO reconciliation state — bookkeeping metadata, allowed in any status.</summary>
    public Result SetQboStatus(string? qboInvoiceId, QboSyncStatus syncStatus)
    {
        QboInvoiceId = string.IsNullOrWhiteSpace(qboInvoiceId) ? null : qboInvoiceId.Trim();
        QboSyncStatus = syncStatus;

        Raise(new InvoiceQboStatusChangedDomainEvent(Id, QboInvoiceId, syncStatus));
        return Result.Success();
    }
}
