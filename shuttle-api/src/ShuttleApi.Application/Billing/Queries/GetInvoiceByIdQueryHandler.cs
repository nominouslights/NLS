using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Billing;

internal sealed class GetInvoiceByIdQueryHandler(IInvoiceRepository invoiceRepository)
    : IRequestHandler<GetInvoiceByIdQuery, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        GetInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {request.InvoiceId} not found.");

        return UpdateInvoiceDraftCommandHandler.MapToDetailResponse(invoice);
    }
}
