using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;

namespace ShuttleApi.Application.Billing;

internal sealed class ListInvoicesByClientQueryHandler(IInvoiceRepository invoiceRepository)
    : IRequestHandler<ListInvoicesByClientQuery, List<InvoiceListItemResponse>>
{
    public async Task<List<InvoiceListItemResponse>> Handle(
        ListInvoicesByClientQuery request,
        CancellationToken cancellationToken)
    {
        InvoiceStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<InvoiceStatus>(request.Status, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        var invoices = await invoiceRepository.GetByClientIdAsync(
            request.ClientId,
            statusFilter,
            cancellationToken);

        return invoices
            .Select(i => new InvoiceListItemResponse(
                i.Id,
                i.InvoiceNumber,
                i.IssuedDate,
                i.DueDate,
                i.Status.ToString(),
                i.TotalAmount))
            .ToList();
    }
}
