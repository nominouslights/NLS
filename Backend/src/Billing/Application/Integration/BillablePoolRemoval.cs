using NorthernLink.Billing.Application.Abstractions;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>What <see cref="BillablePoolRemoval.RemoveUninvoicedAsync"/> found and did.</summary>
internal enum BillablePoolRemovalOutcome
{
    /// <summary>No billable row for the trip — nothing to do (also the redelivery shape).</summary>
    Absent,

    /// <summary>An invoice already claims the row, so it was kept.</summary>
    KeptClaimed,

    /// <summary>The uninvoiced row was deleted and saved.</summary>
    Removed,
}

/// <summary>The outcome, plus the claiming invoice when the row was kept.</summary>
internal sealed record BillablePoolRemovalResult(BillablePoolRemovalOutcome Outcome, Guid? ClaimingInvoiceId = null);

/// <summary>
/// The one "take a trip back out of the billable pool" rule, shared by every Trips event that
/// undoes the billable feed (close-without-billing, reopen): delete the uninvoiced
/// <c>billable_trips</c> row — deletion, not a flag, because absence is what "not billable" means
/// throughout this table; keep a row an invoice already claims, because the claim is the stronger
/// fact; and treat an absent row as a no-op, which is what makes at-least-once delivery
/// idempotent. Callers own the ambient-tenant push and their own logging, so each event still
/// says in the logs why the trip left (or stayed in) the pool.
/// </summary>
internal static class BillablePoolRemoval
{
    public static async Task<BillablePoolRemovalResult> RemoveUninvoicedAsync(
        IBillableTripRepository repository,
        Guid tenantId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await repository.GetAsync(tenantId, tripId, cancellationToken);
        if (trip is null)
        {
            return new BillablePoolRemovalResult(BillablePoolRemovalOutcome.Absent);
        }

        if (trip.InvoiceId is { } invoiceId)
        {
            return new BillablePoolRemovalResult(BillablePoolRemovalOutcome.KeptClaimed, invoiceId);
        }

        repository.Remove(trip);
        await repository.SaveChangesAsync(cancellationToken);
        return new BillablePoolRemovalResult(BillablePoolRemovalOutcome.Removed);
    }
}
