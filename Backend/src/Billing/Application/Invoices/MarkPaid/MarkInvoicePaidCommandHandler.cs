using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.MarkPaid;

public sealed class MarkInvoicePaidCommandHandler(IInvoiceRepository invoices)
    : ICommandHandler<MarkInvoicePaidCommand>
{
    public async Task<Result> Handle(MarkInvoicePaidCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        var result = invoice.MarkPaid();
        if (result.IsFailure)
        {
            return result;
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
