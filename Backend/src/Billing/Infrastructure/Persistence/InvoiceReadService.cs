using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Application.Invoices;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Read side — the worksheet list from <c>billing.rm_invoices</c> (tenant-filtered). No
/// overdue/AR derivation: receivables live in QuickBooks, not the platform.
/// </summary>
internal sealed class InvoiceReadService(BillingDbContext context) : IInvoiceReadService
{
    public async Task<IReadOnlyList<InvoiceSummaryResponse>> GetInvoicesAsync(
        InvoiceStatus? status,
        Guid? clientId,
        CancellationToken cancellationToken = default)
    {
        var query = context.InvoiceReadModels.AsNoTracking();

        if (status is { } statusFilter)
        {
            var statusName = statusFilter.ToString();
            query = query.Where(i => i.Status == statusName);
        }

        if (clientId is { } client)
        {
            query = query.Where(i => i.ClientId == client);
        }

        var invoices = await query
            .OrderByDescending(i => i.IssuedAtUtc)
            .ToListAsync(cancellationToken);

        return invoices.Select(ToResponse).ToList();
    }

    private static InvoiceSummaryResponse ToResponse(InvoiceReadModel i)
    {
        return new InvoiceSummaryResponse(
            i.Id,
            i.InvoiceNumber,
            i.ClientId,
            i.ClientName,
            i.PoNumber,
            i.BudgetCode,
            i.NetTermsDays,
            i.PeriodStart,
            i.PeriodEnd,
            i.Status,
            i.IssuedAtUtc,
            i.SubtotalCad,
            i.GstCad,
            i.TotalCad,
            i.LineCount,
            i.QboInvoiceId,
            i.QboEnteredDate,
            i.PaymentConfirmedDate,
            i.WrittenOffAmountCad,
            i.WrittenOffDate,
            i.WrittenOffReason,
            i.OutstandingCad);
    }
}
