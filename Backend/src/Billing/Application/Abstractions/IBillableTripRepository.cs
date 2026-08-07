using NorthernLink.Billing.Domain.BillableTrips;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>
/// The <c>billable_trips</c> replica. <see cref="ExistsAsync"/> and <see cref="GetAsync"/>
/// take an explicit tenant id because their callers are the integration-event consumers
/// (no ambient tenant); the remaining members run on the request path under the tenant
/// query filter.
/// </summary>
public interface IBillableTripRepository
{
    Task<bool> ExistsAsync(Guid tenantId, Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Tracked single-row fetch for the round-trip re-key consumer; null when the trip was never recorded.</summary>
    Task<BillableTrip?> GetAsync(Guid tenantId, Guid tripId, CancellationToken cancellationToken = default);

    void Add(BillableTrip trip);

    /// <summary>
    /// Drops a trip from the billable pool — the close-without-billing consumer, for a trip that
    /// will never be invoiced. Row deletion, not a flag: absence is what "not billable" means
    /// throughout this table.
    /// </summary>
    void Remove(BillableTrip trip);

    /// <summary>Uninvoiced, round-trip-keyed or not, for a client with service dates inside the period.</summary>
    Task<IReadOnlyList<BillableTrip>> GetUninvoicedForClientAsync(
        Guid clientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillableTrip>> GetByIdsAsync(
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillableTrip>> GetClaimedByInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
