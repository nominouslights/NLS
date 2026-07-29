using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.UpdateQboReference;

public sealed class UpdateInvoiceQboReferenceCommandHandler(IInvoiceRepository invoices)
    : ICommandHandler<UpdateInvoiceQboReferenceCommand>
{
    public async Task<Result> Handle(UpdateInvoiceQboReferenceCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        var result = invoice.UpdateQboReference(command.QboInvoiceNumber, command.EnteredDate);
        if (result.IsFailure)
        {
            return result;
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
