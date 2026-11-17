using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the VehicleInspection aggregate. Unlike the request-path
/// repositories, its callers are integration event handlers running outside any HTTP
/// request (empty ITenantContext), so the existence check takes the tenant explicitly
/// from the event instead of relying on the ambient query filter.
/// </summary>
public interface IVehicleInspectionRepository
{
    /// <summary>True when this manifest already produced an inspection of this type — the consumer's idempotency check.</summary>
    Task<bool> ExistsForManifestAsync(
        Guid tenantId,
        Guid manifestId,
        InspectionType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this trip already has an inspection of this half — the one-pre-trip-and-one-
    /// post-trip-per-trip guard on entry. Takes the tenant explicitly (like
    /// <see cref="ExistsForManifestAsync"/>) so it is correct on both the request path and any
    /// tenant-less caller.
    /// </summary>
    Task<bool> ExistsForTripAsync(
        Guid tenantId,
        string tripNumber,
        InspectionType type,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an inspection by id (tenant-filtered) — used to link a generated work order, amend, or remove.</summary>
    Task<VehicleInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the inspection whose defects generated this work order (tenant-filtered, like
    /// <see cref="GetByIdAsync"/>) — the completion path's source of defects to auto-resolve.
    /// Null is the normal case, not an error: a work order can be raised directly with no DVIR
    /// behind it, and then there is simply nothing to resolve.
    /// </summary>
    Task<VehicleInspection?> GetByGeneratedWorkOrderIdAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The duplicate-defect candidate set, for EVERY component at once: TRACKED inspections
    /// (tenant-filtered, like <see cref="GetByIdAsync"/>) of the vehicle <paramref name="scope"/>
    /// describes that carry at least one UNRESOLVED defect. Deliberately no item predicate — item
    /// matching is the domain's rule (<c>VehicleInspection.IsSameItem</c>, applied in memory by
    /// <c>DuplicateDefects.Find</c>), so a second normalisation in SQL can never wrongly exclude a
    /// row. One query serves any number of items (a work order's lines), and the preview and the
    /// cascade both read through it. May include the source inspection itself.
    /// </summary>
    Task<IReadOnlyList<VehicleInspection>> GetWithOpenDefectsAsync(
        DefectVehicleScope scope,
        CancellationToken cancellationToken = default);

    void Add(VehicleInspection inspection);

    /// <summary>Hard-deletes an inspection. The aggregate must raise its removal event first.</summary>
    void Remove(VehicleInspection inspection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves pending changes, returning false — with nothing persisted and EVERY tracked entity
    /// detached (the aggregates and the snapshot/journal/outbox rows the audit pipeline staged for
    /// them, which rolled back but would otherwise still be tracked as Added) — when an
    /// inspection's Version token was stale: another writer changed it since it was loaded. The
    /// caller re-reads and retries. Any other failure still throws.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets everything staged since the last save by detaching every tracked entity, so a
    /// later save in this scope can never commit a half-applied change or its audit rows.
    /// </summary>
    void DiscardChanges();
}
