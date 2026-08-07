using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Reconciles the trip-billing replica over <see cref="TripsDbContext"/>. Same tenancy shape
/// as <see cref="DriverLookupRepository"/>: the writes run from an integration handler whose
/// DbContext was constructed before the handler pushed the event's tenant, so the context's
/// captured TenantId is null and the query filter would match nothing — every statement
/// bypasses the filter and matches on tenant explicitly. Postgres RLS still scopes it, since
/// the session variable is read at connection open inside the ambient push.
/// </summary>
internal sealed class TripBillingRepository(TripsDbContext context) : ITripBillingRepository
{
    public async Task<IReadOnlyList<TripBilling>> GetByInvoiceAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        await context.TripBillings
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripBilling>> GetByTripIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default)
    {
        if (tripIds.Count == 0)
        {
            return [];
        }

        return await context.TripBillings
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && tripIds.Contains(b.TripId))
            .ToListAsync(cancellationToken);
    }

    public async Task ReconcileAsync(
        Guid tenantId,
        Guid invoiceId,
        IReadOnlyList<TripBilling> claimed,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.TripBillings
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

        var existingByTrip = existing.ToDictionary(b => b.TripId);
        var claimedTripIds = claimed.Select(b => b.TripId).ToHashSet();

        foreach (var row in claimed)
        {
            if (existingByTrip.TryGetValue(row.TripId, out var current))
            {
                // High-water mark: this row already reflects a later event, so the arriving one
                // is a replay or arrived out of order. Leave it entirely alone.
                if (current.UpdatedAtUtc > occurredAtUtc)
                {
                    continue;
                }

                current.InvoiceNumber = row.InvoiceNumber;
                current.State = row.State;
                current.QboInvoiceId = row.QboInvoiceId;
                current.QboEnteredDate = row.QboEnteredDate;
                current.PaymentConfirmedDate = row.PaymentConfirmedDate;
                current.WrittenOffReason = row.WrittenOffReason;
                current.UpdatedAtUtc = row.UpdatedAtUtc;
                continue;
            }

            // A trip claimed by a DIFFERENT invoice cannot be stolen here — Billing's
            // billable_trips claim already prevents double-claiming upstream, so any row we
            // find under another invoice id is stale and simply gets replaced by this one.
            var stale = await context.TripBillings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.TripId == row.TripId && b.TenantId == tenantId, cancellationToken);

            if (stale is not null)
            {
                context.TripBillings.Remove(stale);
            }

            context.TripBillings.Add(row);
        }

        // Anything still pointing at this invoice but absent from its claim set was released:
        // a line was removed, or the draft was voided. The trip is billable again — unless the
        // row is newer than this event, in which case the release is stale news.
        foreach (var row in existing.Where(b =>
                     !claimedTripIds.Contains(b.TripId) && b.UpdatedAtUtc <= occurredAtUtc))
        {
            context.TripBillings.Remove(row);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
