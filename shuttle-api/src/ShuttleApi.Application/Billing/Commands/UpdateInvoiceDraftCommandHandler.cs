using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Billing;

internal sealed class UpdateInvoiceDraftCommandHandler(IInvoiceRepository invoiceRepository)
    : IRequestHandler<UpdateInvoiceDraftCommand, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        UpdateInvoiceDraftCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {request.InvoiceId} not found.");

        invoice.UpdateDraft(request.DueDate, request.Notes);

        foreach (var dto in request.LineItems)
        {
            var line = invoice.LineItems.FirstOrDefault(l => l.Id == dto.Id)
                ?? throw new NotFoundException($"Line item {dto.Id} not found on invoice.");
            line.Update(dto.Description, dto.UnitRate, dto.Quantity);
        }

        await invoiceRepository.UpdateAsync(invoice, cancellationToken);

        return MapToDetailResponse(invoice);
    }

    internal static InvoiceDetailResponse MapToDetailResponse(Invoice invoice)
    {
        var lineItems = invoice.LineItems
            .OrderBy(l => l.SortOrder)
            .Select(l => new InvoiceLineItemResponse(
                l.Id,
                l.TripId,
                l.LineType.ToString(),
                l.Description,
                l.UnitRate,
                l.Quantity,
                l.LineTotal,
                l.SortOrder))
            .ToList();

        return new InvoiceDetailResponse(
            invoice.Id,
            invoice.ClientId,
            invoice.InvoiceNumber,
            invoice.Status.ToString(),
            invoice.IssuedDate,
            invoice.DueDate,
            invoice.Notes,
            invoice.PaidAt,
            invoice.SubTotal,
            invoice.TotalAmount,
            lineItems);
    }
}
