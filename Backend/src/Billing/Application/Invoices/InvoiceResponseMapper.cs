using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Application.Invoices;

/// <summary>
/// Maps the Invoice aggregate to its detail response. The single implementation of the
/// overdue derivation: <c>Sent &amp;&amp; today &gt; SentAt + NetTermsDays</c> — the read
/// service applies the same formula to rm rows.
/// </summary>
public static class InvoiceResponseMapper
{
    public static InvoiceResponse ToResponse(Invoice invoice)
    {
        var (dueDate, isOverdue) = DeriveDue(invoice.Status.ToString(), invoice.SentAtUtc, invoice.NetTermsDays);

        return new InvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.ClientId,
            invoice.ClientName,
            invoice.ContractId,
            invoice.PoNumber,
            invoice.BudgetCode,
            invoice.NetTermsDays,
            invoice.GstApplicable,
            invoice.GstRate,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.Status.ToString(),
            invoice.IssuedAtUtc,
            invoice.SentAtUtc,
            invoice.PaidAtUtc,
            dueDate,
            isOverdue,
            invoice.SubtotalCad,
            invoice.GstCad,
            invoice.TotalCad,
            invoice.QboInvoiceId,
            invoice.QboSyncStatus.ToString(),
            invoice.Lines
                .Select(line => new InvoiceLineResponse(
                    line.LineId,
                    line.Description,
                    line.TripIds,
                    line.TripNumber,
                    line.ServiceDate,
                    line.Quantity,
                    line.UnitPriceCad,
                    line.AmountCad))
                .ToList());
    }

    /// <summary>Due date and overdue flag from the sent timestamp and net terms; null/false until sent.</summary>
    public static (DateOnly? DueDate, bool IsOverdue) DeriveDue(
        string status,
        DateTimeOffset? sentAtUtc,
        int netTermsDays)
    {
        if (sentAtUtc is not { } sentAt)
        {
            return (null, false);
        }

        var dueDate = DateOnly.FromDateTime(sentAt.UtcDateTime).AddDays(netTermsDays);
        var isOverdue = status == nameof(InvoiceStatus.Sent)
            && DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime) > dueDate;

        return (dueDate, isOverdue);
    }
}
