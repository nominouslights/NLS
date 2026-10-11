using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Vendor aggregate (tenant-scoped through the query filter and
/// RLS beneath it, so another tenant's vendor reads back as null). Vendors are tenant-wide, so
/// no lookup names a period.
/// </summary>
public interface IVendorRepository
{
    Task<Vendor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's vendors (active or retired) among these ids, in one round trip
    /// (<c>WHERE id = ANY(@ids)</c>), in no particular order. An id with no vendor in this tenant is
    /// simply absent from the result — the caller decides what a missing one means. No query at
    /// all for an empty set.
    /// </summary>
    Task<IReadOnlyList<Vendor>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's vendor (active or retired) whose <see cref="Vendor.NormalizedName"/> equals
    /// this already-normalized string, or null. Retired vendors count: the unique index covers
    /// them too, and re-adding a retired vendor should be a reactivation, not a second row.
    /// </summary>
    Task<Vendor?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);

    void Add(Vendor vendor);

    /// <summary>Hard delete — the caller is responsible for the usage guard; see the delete handler.</summary>
    void Remove(Vendor vendor);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves like <see cref="SaveChangesAsync"/>, but returns false — instead of throwing — when
    /// the save lost a race on the unique (tenant_id, normalized_name) index: another request
    /// claimed the name between the handler's <see cref="GetByNormalizedNameAsync"/> check and
    /// this commit. On false nothing was persisted and the unit of work has been cleared, so the
    /// caller can re-read (see <see cref="Vendors.VendorNameRule.ConflictAfterLostRaceAsync"/>)
    /// and report a 409. Any other failure still throws.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's <b>vendor register lock</b> — the <see cref="ICostCentreRepository.LockRegisterAsync"/>
    /// shape, with its own key. It closes one race: a vendor delete passing its usage probe while
    /// a budget item starts referencing that vendor, which would leave a share naming a vendor that
    /// no longer exists. Taken by vendor delete (around the probe and the remove) and by every item
    /// write that <b>adds</b> a vendor share — create, an update naming a vendor the item did not
    /// have, and a copy-from-period whose planned copies carry shares — before its register lookup. Writes that only keep shares need
    /// no lock: their references are already committed, so the delete's probe sees them.
    /// <para>
    /// Opens a transaction on this unit of work (unless one is already open — then the caller's
    /// transaction decides and the scope's commit is a no-op) and takes a transaction-scoped
    /// advisory lock keyed on the tenant, held until the scope commits or is disposed (rollback).
    /// Take it before the first register read, then read, check, write and commit inside it.
    /// </para>
    /// <para>
    /// Not taken by activate/deactivate: retiring is lenient by design (a share already on an
    /// item stays valid), so a retire racing an add has the same outcome as the add committing
    /// first — an item with a share on a retired vendor, which the model already allows.
    /// </para>
    /// </summary>
    Task<IVendorRegisterLock> LockRegisterAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The scope <see cref="IVendorRepository.LockRegisterAsync"/> returns. Committing releases the
/// lock together with the writes made under it; disposing without committing rolls back and
/// releases it.
/// </summary>
public interface IVendorRegisterLock : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
