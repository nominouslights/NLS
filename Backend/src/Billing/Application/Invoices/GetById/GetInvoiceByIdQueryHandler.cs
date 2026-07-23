using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GetById;

/// <summary>
/// Detail reads the aggregate (tenant-filtered), not <c>rm_invoices</c>: the line-editing
/// screen needs read-your-writes — a PUT followed by a GET must show the new lines, and
/// the projection worker is asynchronous. The list stays on the read model.
/// </summary>
public sealed class GetInvoiceByIdQueryHandler(IInvoiceRepository invoices)
    : IQueryHandler<GetInvoiceByIdQuery, InvoiceResponse>
{
    public async Task<Result<InvoiceResponse>> Handle(
        GetInvoiceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(query.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<InvoiceResponse>(InvoiceErrors.NotFound);
        }

        return Result.Success(InvoiceResponseMapper.ToResponse(invoice));
    }
}
