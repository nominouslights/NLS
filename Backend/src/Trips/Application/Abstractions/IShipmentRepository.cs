using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Shipment aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS) and always load
/// the aggregate's legs, since every command that touches routing needs them.
/// </summary>
public interface IShipmentRepository
{
    void Add(Shipment shipment);

    Task<Shipment?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    Task<Shipment?> GetByNumberAsync(string shipmentNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk load for the bulk-assign command, which routes a whole Friday run's worth of freight
    /// in one action. Missing ids are simply absent from the result rather than an error.
    /// </summary>
    Task<IReadOnlyList<Shipment>> GetByIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every shipment with a leg on the given trip — the reaction to a cancelled trip loads
    /// these to release their planned legs.
    /// </summary>
    Task<IReadOnlyList<Shipment>> GetForTripAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many shipments are booked on a trip. Backs the relaxed en-route gate: a freight-only
    /// run has no passengers and is still a legitimate run.
    /// </summary>
    Task<int> CountForTripAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk load for the billing-state consumer, mirroring <c>ITripRepository.GetByIdsAsync</c>:
    /// takes the tenant explicitly and bypasses the query filter, because the DbContext was built
    /// before the handler pushed the event's tenant and its captured TenantId is null. Postgres
    /// RLS still scopes the statement — the session variable is read at connection open inside
    /// the ambient push.
    /// </summary>
    Task<IReadOnlyList<Shipment>> GetByIdsForTenantAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
