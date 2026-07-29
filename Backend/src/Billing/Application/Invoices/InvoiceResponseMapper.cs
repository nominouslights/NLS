using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Application.Invoices;

/// <summary>
/// Maps the Invoice aggregate to its detail response. Receivables (sent/paid/overdue) are
/// QuickBooks' concern now — the platform exposes only the worksheet numbers and the manual
/// QBO reference.
/// </summary>
public static class InvoiceResponseMapper
{
    public static InvoiceResponse ToResponse(Invoice invoice)
    {
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
            invoice.SubtotalCad,
            invoice.GstCad,
            invoice.TotalCad,
            invoice.QboInvoiceId,
            invoice.QboEnteredDate,
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
}
