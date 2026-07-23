using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GetInvoices;

public sealed class GetInvoicesQueryHandler(IInvoiceReadService readService)
    : IQueryHandler<GetInvoicesQuery, IReadOnlyList<InvoiceSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<InvoiceSummaryResponse>>> Handle(
        GetInvoicesQuery query,
        CancellationToken cancellationToken)
    {
        var invoices = await readService.GetInvoicesAsync(query.Status, query.ClientId, cancellationToken);
        return Result.Success(invoices);
    }
}
