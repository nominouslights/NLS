using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.Send;

public sealed class SendInvoiceCommandHandler(IInvoiceRepository invoices)
    : ICommandHandler<SendInvoiceCommand>
{
    public async Task<Result> Handle(SendInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        var result = invoice.Send();
        if (result.IsFailure)
        {
            return result;
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
