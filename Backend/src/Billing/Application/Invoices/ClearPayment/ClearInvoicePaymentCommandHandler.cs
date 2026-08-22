using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.ClearPayment;

public sealed class ClearInvoicePaymentCommandHandler(IInvoiceRepository invoices)
    : ICommandHandler<ClearInvoicePaymentCommand>
{
    public async Task<Result> Handle(ClearInvoicePaymentCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        var result = invoice.ClearPaymentConfirmation();
        if (result.IsFailure)
        {
            return result;
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
