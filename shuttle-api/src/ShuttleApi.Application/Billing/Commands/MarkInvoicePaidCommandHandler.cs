using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Billing;

internal sealed class MarkInvoicePaidCommandHandler(
    IInvoiceRepository invoiceRepository,
    IClientRepository clientRepository)
    : IRequestHandler<MarkInvoicePaidCommand, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        MarkInvoicePaidCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {request.InvoiceId} not found.");

        var totalAmount = invoice.TotalAmount;

        var result = invoice.MarkPaid();
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error);

        await invoiceRepository.UpdateAsync(invoice, cancellationToken);

        var client = await clientRepository.GetByIdAsync(invoice.ClientId, cancellationToken);
        if (client is not null)
        {
            var newBalance = client.OutstandingBalance - totalAmount;
            client.SetOutstandingBalance(newBalance);
            await clientRepository.UpdateAsync(client, cancellationToken);
        }

        return UpdateInvoiceDraftCommandHandler.MapToDetailResponse(invoice);
    }
}
