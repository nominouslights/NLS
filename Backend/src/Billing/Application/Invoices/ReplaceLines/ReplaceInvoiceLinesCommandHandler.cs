using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.ReplaceLines;

/// <summary>
/// Draft line editing with claim reconciliation: trips referenced only by removed lines
/// get their <c>invoice_id</c> cleared; trips newly referenced get claimed — but only if
/// they exist for this tenant and are not already on another invoice. Everything commits
/// in one SaveChanges, so lines and claims can never drift.
/// </summary>
public sealed class ReplaceInvoiceLinesCommandHandler(
    IInvoiceRepository invoices,
    IBillableTripRepository billableTrips)
    : ICommandHandler<ReplaceInvoiceLinesCommand>
{
    public async Task<Result> Handle(ReplaceInvoiceLinesCommand command, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(command.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(InvoiceErrors.NotFound);
        }

        var newLines = new List<InvoiceLine>(command.Lines.Count);
        foreach (var input in command.Lines)
        {
            var lineResult = InvoiceLine.Create(
                input.Description,
                input.TripIds,
                input.TripNumber,
                input.ServiceDate,
                input.Quantity,
                input.UnitPriceCad,
                input.LineId);

            if (lineResult.IsFailure)
            {
                return lineResult;
            }

            newLines.Add(lineResult.Value);
        }

        var oldTripIds = invoice.Lines.SelectMany(line => line.TripIds).ToHashSet();
        var newTripIds = newLines.SelectMany(line => line.TripIds).ToHashSet();

        // Newly referenced trips must exist and be claimable before anything mutates.
        var addedIds = newTripIds.Except(oldTripIds).ToList();
        var addedTrips = await billableTrips.GetByIdsAsync(addedIds, cancellationToken);
        var addedById = addedTrips.ToDictionary(t => t.Id);

        foreach (var tripId in addedIds)
        {
            if (!addedById.TryGetValue(tripId, out var trip))
            {
                return Result.Failure(InvoiceErrors.TripNotFound(tripId));
            }

            if (trip.InvoiceId is { } claimedBy && claimedBy != invoice.Id)
            {
                return Result.Failure(InvoiceErrors.TripAlreadyInvoiced(tripId));
            }
        }

        var replaceResult = invoice.ReplaceLines(newLines);
        if (replaceResult.IsFailure)
        {
            return replaceResult;
        }

        var releasedIds = oldTripIds.Except(newTripIds).ToList();
        var releasedTrips = await billableTrips.GetByIdsAsync(releasedIds, cancellationToken);
        foreach (var trip in releasedTrips.Where(t => t.InvoiceId == invoice.Id))
        {
            trip.Release();
        }

        foreach (var trip in addedTrips)
        {
            trip.ClaimFor(invoice.Id);
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
