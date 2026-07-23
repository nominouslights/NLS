namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Issues the next human-facing trip number ("TR-4821") from a per-tenant sequence —
/// unique per tenant (backstopped by the unique index on (tenant_id, trip_number)).
/// The Infrastructure implementation increments an atomic counter row in the trips
/// schema, outside the aggregate save's transaction: a failed save may burn a number,
/// which is acceptable — trip numbers are unique, not gapless.
/// </summary>
public interface ITripNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
