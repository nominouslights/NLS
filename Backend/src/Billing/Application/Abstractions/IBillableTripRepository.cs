using NorthernLink.Billing.Domain.BillableTrips;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>
/// The <c>billable_trips</c> replica. <see cref="ExistsAsync"/> takes an explicit tenant
/// id because its caller is the integration-event consumer (no ambient tenant); the
/// remaining members run on the request path under the tenant query filter.
/// </summary>
public interface IBillableTripRepository
{
    Task<bool> ExistsAsync(Guid tenantId, Guid tripId, CancellationToken cancellationToken = default);

    void Add(BillableTrip trip);

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
