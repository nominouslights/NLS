using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.Void;

/// <summary>
/// Voiding a draft returns its claimed billable trips to the uninvoiced pool in the same
/// SaveChanges — the trips are immediately available to the next generated draft.
/// </summary>
public sealed class VoidInvoiceCommandHandler(
    IInvoiceRepository invoices,
    IBillableTripRepository billableTrips)
    : ICommandHandler<VoidInvoiceCommand>
{
    public async Task<Result> Handle(VoidInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        var result = invoice.Void();
        if (result.IsFailure)
        {
            return result;
        }

        var claimedTrips = await billableTrips.GetClaimedByInvoiceAsync(invoice.Id, cancellationToken);
        foreach (var trip in claimedTrips)
        {
            trip.Release();
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
