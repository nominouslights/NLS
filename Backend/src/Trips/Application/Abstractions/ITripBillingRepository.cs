using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Reconcile access to the trip-billing replica. Deliberately invoice-shaped rather than
/// trip-shaped: the producer publishes an invoice's whole claim set, and the only way to
/// notice a <em>released</em> trip is to sweep everything still pointing at that invoice.
/// </summary>
public interface ITripBillingRepository
{
    /// <summary>Every replica row currently claimed by this invoice, for the given tenant.</summary>
    Task<IReadOnlyList<TripBilling>> GetByInvoiceAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The replica rows for a specific set of trips, whichever invoice claims them. Used to read
    /// each trip's high-water mark before deciding whether an arriving event is fresh enough to
    /// act on.
    /// </summary>
    Task<IReadOnlyList<TripBilling>> GetByTripIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an invoice's complete claim set: upserts a row for every trip in
    /// <paramref name="claimed"/> and deletes any row for this invoice that is not in it.
    /// Idempotent — replaying the same event is a no-op.
    /// <para>
    /// <paramref name="occurredAtUtc"/> is the arriving event's timestamp and acts as a
    /// high-water mark: a row already stamped later than it is left completely alone, neither
    /// updated nor swept. Delivery is at-least-once with no global ordering, and because
    /// Completed → Invoiced is a legal trip transition, a replayed older event would otherwise
    /// walk a settled trip backwards. The same guard lives here rather than in the caller so the
    /// replica and the trip aggregate can never disagree about which event won.
    /// </para>
    /// </summary>
    Task ReconcileAsync(
        Guid tenantId,
        Guid invoiceId,
        IReadOnlyList<TripBilling> claimed,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);
}
