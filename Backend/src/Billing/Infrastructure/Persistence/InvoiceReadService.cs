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

    public async Task<IReadOnlyDictionary<string, decimal>> GetInvoicedTotalsByPoNumberAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var voidStatus = InvoiceStatus.Void.ToString();

        var rows = await context.InvoiceReadModels
            .AsNoTracking()
            .Where(i => i.ClientId == clientId && i.PoNumber != null && i.Status != voidStatus)
            .Select(i => new { i.PoNumber, i.TotalCad })
            .ToListAsync(cancellationToken);

        // Grouped in memory with an ordinal-ignore-case comparer: Postgres would fold case with
        // its own collation rules, and the builder matches PO numbers case-insensitively too.
        return rows
            .GroupBy(r => r.PoNumber!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.TotalCad), 2), StringComparer.OrdinalIgnoreCase);
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
