using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.WriteOff;

public sealed class WriteOffInvoiceCommandHandler(IInvoiceRepository invoices)
    : ICommandHandler<WriteOffInvoiceCommand>
{
    public async Task<Result> Handle(WriteOffInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        // No billable-trip release here, unlike Void: the runs happened and were billed once.
        var result = invoice.WriteOff(command.AmountCad, command.WrittenOffDate, command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
