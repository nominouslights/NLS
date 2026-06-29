using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Billing;

internal sealed class VoidInvoiceCommandHandler(IInvoiceRepository invoiceRepository)
    : IRequestHandler<VoidInvoiceCommand, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        VoidInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {request.InvoiceId} not found.");

        var result = invoice.Void();
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error);

        await invoiceRepository.UpdateAsync(invoice, cancellationToken);

        return UpdateInvoiceDraftCommandHandler.MapToDetailResponse(invoice);
    }
}
