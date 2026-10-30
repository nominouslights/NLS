using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GenerateDraft;

/// <summary>
/// Draft generation: resolve the client's contract snapshots, PO snapshots and uninvoiced
/// completed trips, let the pure <see cref="InvoiceDraftBuilder"/> price the period, then
/// persist <b>one worksheet per purchase order</b> and stamp <c>invoice_id</c> onto the claimed
/// billable trips in a single SaveChanges — the repositories share the module's scoped
/// DbContext, so every claim and every invoice commits atomically, all or nothing.
/// <para>
/// Each worksheet stamps its <em>own</em> PO number (not the contract default any more) and
/// draws its own invoice number from one reserved batch, so numbers inside a generation cannot
/// collide. The builder's per-PO grouping partitions the eligible trips, so no trip can appear
/// in two drafts — the "claimed by at most one invoice" guarantee survives the split, and the
/// assertion below makes that structural rather than assumed.
/// </para>
/// Budget code and net terms still default from the contract snapshot; trips without a
/// round-trip key stay unclaimed for manual lines; a zero-line draft is allowed (the
/// manual-lines starting point).
/// </summary>
public sealed class GenerateDraftInvoiceCommandHandler(
    IContractSnapshotRepository contractSnapshots,
    IPurchaseOrderSnapshotRepository purchaseOrderSnapshots,
    IBillableTripRepository billableTrips,
    IInvoiceRepository invoices,
    IInvoiceReadService invoiceReads,
    IInvoiceNumberGenerator invoiceNumbers)
    : ICommandHandler<GenerateDraftInvoiceCommand, GenerateDraftInvoicesResult>
{
    public async Task<Result<GenerateDraftInvoicesResult>> Handle(
        GenerateDraftInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var contracts = await contractSnapshots.GetForClientAsync(command.ClientId, cancellationToken);
        var purchaseOrders = await purchaseOrderSnapshots.GetForClientAsync(command.ClientId, cancellationToken);
        var trips = await billableTrips.GetUninvoicedForClientAsync(
            command.ClientId, command.PeriodStart, command.PeriodEnd, cancellationToken);
        var invoicedByPo = await invoiceReads.GetInvoicedTotalsByPoNumberAsync(
            command.ClientId, cancellationToken);

        var draftsResult = InvoiceDraftBuilder.Build(
            contracts, command.PeriodStart, command.PeriodEnd, trips, purchaseOrders, invoicedByPo);

        if (draftsResult.IsFailure)
        {
            return Result.Failure<GenerateDraftInvoicesResult>(draftsResult.Error);
        }

        var drafts = draftsResult.Value;
        var numbers = await invoiceNumbers.NextInvoiceNumbersAsync(
            command.TenantId, drafts.Count, cancellationToken);

        var generated = new List<GeneratedInvoiceDraft>(drafts.Count);
        var claimedAcrossDrafts = new HashSet<Guid>();

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];

            var invoiceResult = Invoice.CreateDraft(
                command.TenantId,
                numbers[i],
                command.ClientId,
                draft.Contract.ClientName,
                draft.Contract.Id,
                draft.PoNumber,
                draft.Contract.BudgetCode,
                draft.Contract.NetTermsDays,
                command.PeriodStart,
                command.PeriodEnd,
                draft.Lines);

            if (invoiceResult.IsFailure)
            {
                // All-or-nothing: nothing has been saved yet, so returning here leaves the
                // database untouched rather than half a set of worksheets.
                return Result.Failure<GenerateDraftInvoicesResult>(invoiceResult.Error);
            }

            var invoice = invoiceResult.Value;
            invoices.Add(invoice);

            foreach (var tripId in draft.ClaimedTripIds)
            {
                if (!claimedAcrossDrafts.Add(tripId))
                {
                    // Structurally impossible — the builder partitions trips by PO — but a trip
                    // billed on two worksheets is the one bug in here nobody would notice, so it
                    // fails loudly instead of silently double-billing.
                    return Result.Failure<GenerateDraftInvoicesResult>(InvoiceErrors.TripClaimedTwice);
                }
            }

            var claimed = draft.ClaimedTripIds.ToHashSet();
            foreach (var trip in trips.Where(t => claimed.Contains(t.Id)))
            {
                trip.ClaimFor(invoice.Id);
            }

            generated.Add(new GeneratedInvoiceDraft(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.PoNumber,
                invoice.Lines.Count,
                invoice.TotalCad,
                draft.Warnings));
        }

        await invoices.SaveChangesAsync(cancellationToken);
        return Result.Success(new GenerateDraftInvoicesResult(generated));
    }
}
