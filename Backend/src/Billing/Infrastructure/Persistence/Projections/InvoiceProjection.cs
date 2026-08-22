using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Billing.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Invoice"/> into <c>billing.rm_invoices</c>. Totals are read straight
/// off the aggregate's computed properties, so the API and the read model can never disagree
/// about a rounded value. Consumes every Invoice domain event (drafted, lines replaced,
/// entered-in-QBO, status-changed for Void/Reopen) — the projector re-maps the whole row from
/// the current aggregate state each time, so no per-event branching is needed.
/// </summary>
internal sealed class InvoiceProjection : BillingProjection<Invoice, InvoiceReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(Invoice));

    protected override void Map(Invoice source, InvoiceReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.InvoiceNumber = source.InvoiceNumber;
        row.ClientId = source.ClientId;
        row.ClientName = source.ClientName;
        row.ContractId = source.ContractId;
        row.PoNumber = source.PoNumber;
        row.BudgetCode = source.BudgetCode;
        row.NetTermsDays = source.NetTermsDays;
        row.GstApplicable = source.GstApplicable;
        row.GstRate = source.GstRate;
        row.PeriodStart = source.PeriodStart;
        row.PeriodEnd = source.PeriodEnd;
        row.Status = source.Status.ToString();
        row.IssuedAtUtc = source.IssuedAtUtc;
        row.QboInvoiceId = source.QboInvoiceId;
        row.QboEnteredDate = source.QboEnteredDate;
        row.PaymentConfirmedDate = source.PaymentConfirmedDate;
        row.WrittenOffAmountCad = source.WrittenOffAmountCad;
        row.WrittenOffDate = source.WrittenOffDate;
        row.WrittenOffReason = source.WrittenOffReason;
        row.OutstandingCad = source.OutstandingCad;
        row.Version = source.Version;

        // Derived — the aggregate is the only implementation.
        row.SubtotalCad = source.SubtotalCad;
        row.GstCad = source.GstCad;
        row.TotalCad = source.TotalCad;
        row.LineCount = source.Lines.Count;
    }
}
