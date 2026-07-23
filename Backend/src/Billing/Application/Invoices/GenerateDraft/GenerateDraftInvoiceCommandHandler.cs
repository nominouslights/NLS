using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GenerateDraft;

/// <summary>
/// Draft generation: resolve the client's contract snapshots and uninvoiced completed
/// trips, let the pure <see cref="InvoiceDraftBuilder"/> price the period, then persist
/// the invoice and stamp <c>invoice_id</c> onto the claimed billable trips in one
/// SaveChanges — the repositories share the module's scoped DbContext, so the claim and
/// the invoice commit atomically. Trips without a round-trip key stay unclaimed for
/// manual lines on the draft. PO number and budget code default from the contract
/// snapshot; a zero-line draft is allowed (the manual-lines starting point).
/// </summary>
public sealed class GenerateDraftInvoiceCommandHandler(
    IContractSnapshotRepository contractSnapshots,
    IBillableTripRepository billableTrips,
    IInvoiceRepository invoices,
    IInvoiceNumberGenerator invoiceNumbers)
    : ICommandHandler<GenerateDraftInvoiceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GenerateDraftInvoiceCommand command, CancellationToken cancellationToken)
    {
        var contracts = await contractSnapshots.GetForClientAsync(command.ClientId, cancellationToken);
        var trips = await billableTrips.GetUninvoicedForClientAsync(
            command.ClientId, command.PeriodStart, command.PeriodEnd, cancellationToken);

        var draftResult = InvoiceDraftBuilder.Build(contracts, command.PeriodStart, command.PeriodEnd, trips);
        if (draftResult.IsFailure)
        {
            return Result.Failure<Guid>(draftResult.Error);
        }

        var draft = draftResult.Value;
        var invoiceNumber = await invoiceNumbers.NextInvoiceNumberAsync(command.TenantId, cancellationToken);

        var invoiceResult = Invoice.CreateDraft(
            command.TenantId,
            invoiceNumber,
            command.ClientId,
            draft.Contract.ClientName,
            draft.Contract.Id,
            draft.Contract.DefaultPoNumber,
            draft.Contract.BudgetCode,
            draft.Contract.NetTermsDays,
            draft.Contract.GstApplicable,
            Invoice.StandardGstRate,
            command.PeriodStart,
            command.PeriodEnd,
            draft.Lines);

        if (invoiceResult.IsFailure)
        {
            return Result.Failure<Guid>(invoiceResult.Error);
        }

        var invoice = invoiceResult.Value;
        invoices.Add(invoice);

        var claimed = draft.ClaimedTripIds.ToHashSet();
        foreach (var trip in trips.Where(t => claimed.Contains(t.Id)))
        {
            trip.ClaimFor(invoice.Id);
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success(invoice.Id);
    }
}
