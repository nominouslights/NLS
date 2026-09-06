using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Trip aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface ITripRepository
{
    void Add(Trip trip);

    Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Looks a trip up by its human trip number — unique per tenant.</summary>
    Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default);

    /// <summary>All legs sharing a round-trip key (normally two) — unpair clears every one.</summary>
    Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(string roundTripKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk load for the billing-state consumer: one invoice's claim set can be dozens of trips,
    /// and they all advance together off a single event. Missing ids are simply absent from the
    /// result rather than an error — a claim can outlive a deleted trip.
    /// <para>
    /// Takes the tenant explicitly and bypasses the query filter, the consumer-path shape used by
    /// <c>TripBillingRepository</c>: the DbContext was built before the handler pushed the event's
    /// tenant, so its captured TenantId is null and the filter would match nothing. Postgres RLS
    /// still scopes the statement, since the session variable is read at connection open inside
    /// the ambient push.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Trip>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The trip materialized from one booking day, when it exists — the cheap pre-check the
    /// booking-day-confirmed consumer runs before minting a trip number. Explicit tenant +
    /// filter bypass (the consumer-path shape — see <see cref="GetByIdsAsync"/>).
    /// </summary>
    Task<Trip?> GetByBookingDayIdAsync(
        Guid tenantId,
        Guid bookingDayId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds and SAVES a booking-sourced trip, absorbing the (tenant_id, booking_day_id)
    /// unique-index race DB-atomically: false when a concurrent (or replayed) processing of
    /// the same confirmation already created the day's trip — the caller then no-ops, per
    /// the single-API-instance rule that idempotency guarantees live in the database, never
    /// in an application-level existence check alone.
    /// </summary>
    Task<bool> TryAddForBookingDayAsync(Trip trip, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
