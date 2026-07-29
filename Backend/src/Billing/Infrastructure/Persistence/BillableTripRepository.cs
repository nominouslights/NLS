using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.BillableTrips;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// The <c>billable_trips</c> replica over <see cref="BillingDbContext"/>. The existence
/// check is called by the integration-event consumer (no ambient tenant), so it bypasses
/// the query filter and matches the event's tenant id explicitly — the Fleet
/// inspection-consumer pattern. Everything else runs on the request path behind the
/// tenant filter.
/// </summary>
internal sealed class BillableTripRepository(BillingDbContext context) : IBillableTripRepository
{
    public Task<bool> ExistsAsync(Guid tenantId, Guid tripId, CancellationToken cancellationToken = default) =>
        context.BillableTrips
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && t.Id == tripId, cancellationToken);

    public Task<BillableTrip?> GetAsync(Guid tenantId, Guid tripId, CancellationToken cancellationToken = default) =>
        context.BillableTrips
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == tripId, cancellationToken);

    public void Add(BillableTrip trip) => context.BillableTrips.Add(trip);

    public async Task<IReadOnlyList<BillableTrip>> GetUninvoicedForClientAsync(
        Guid clientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default) =>
        await context.BillableTrips
            .Where(t => t.ClientId == clientId
                && t.InvoiceId == null
                && t.ServiceDate >= periodStart
                && t.ServiceDate <= periodEnd)
            .OrderBy(t => t.ServiceDate)
            .ThenBy(t => t.CompletedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BillableTrip>> GetByIdsAsync(
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default) =>
        tripIds.Count == 0
            ? []
            : await context.BillableTrips
                .Where(t => tripIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BillableTrip>> GetClaimedByInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        await context.BillableTrips
            .Where(t => t.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
