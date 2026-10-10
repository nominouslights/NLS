using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Write-side persistence for the CostCentre aggregate (tenant-scoped through the DbContext's
/// query filter, RLS beneath it). The register is tenant-wide, so no lookup names a period.
/// </summary>
public interface ICostCentreRepository
{
    /// <summary>The tenant's cost centre with that id, or null (including another tenant's id).</summary>
    Task<CostCentre?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's cost centre with that exact (already normalized — trimmed) code, compared
    /// ordinally, or null. Serves the duplicate check and the budget-code validation.
    /// </summary>
    Task<CostCentre?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);

    /// <summary>Whether any cost centre (active or retired) rolls up into this one.</summary>
    Task<bool> HasChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>Whether any <b>active</b> cost centre rolls up into this one — the deactivation guard.</summary>
    Task<bool> HasActiveChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    void Add(CostCentre costCentre);

    /// <summary>Hard delete — the caller runs the children and usage guards first, under <see cref="LockRegisterAsync"/>.</summary>
    void Remove(CostCentre costCentre);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves like <see cref="SaveChangesAsync"/>, but returns false (it does not throw) when
    /// the save lost a race on the unique (tenant_id, code) index: another request created the
    /// same code between the handler's <see cref="GetByCodeAsync"/> check and this commit. Creates
    /// run under <see cref="LockRegisterAsync"/>, so this is the backstop for a writer that skipped
    /// the lock (a hand-run script), not the expected path. On false
    /// nothing was persisted and the unit of work has been cleared, so the caller can re-read the
    /// winner and report the normal 409. Any other failure still throws. The
    /// <see cref="IVendorRepository.TrySaveChangesAsync"/> shape.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's <b>cost-centre register lock</b>: one lock per tenant that serializes every
    /// write whose checks read the register's integrity. Those writes are cost-centre create
    /// (parent check), update (parent and children checks), activate/deactivate (parent and
    /// children checks) and delete (children and usage checks), plus every budget-code write that
    /// validates or carries a cost-centre string: create, an update that changes the cost centre,
    /// and copy-from-period.
    /// <para>
    /// Opens a transaction on this unit of work (unless one is already open, in which case the
    /// caller's transaction decides and the scope's commit is a no-op) and takes a
    /// transaction-scoped lock keyed on the tenant. It is held until the returned scope commits,
    /// or is disposed without committing (rollback). The caller must therefore take it
    /// <em>before</em> its first register read, then read, check, write and
    /// <see cref="ICostCentreRegisterLock.CommitAsync"/> inside it. A check made before the lock
    /// can be invalidated by a concurrent write and is worth nothing.
    /// </para>
    /// <para>
    /// <b>Why one lock per tenant rather than per code.</b> The races span rows: restoring a child
    /// while its parent is retired, adding a child to an entry that is being deleted, a budget code
    /// starting to carry a string that is being deleted. A per-row key would need every path to
    /// know, and lock in a fixed order, every row its checks touch. The register is low-volume
    /// (a handful of entries edited a few times a period), so serializing these writes per tenant
    /// costs nothing a user can notice and leaves no cross-row window.
    /// </para>
    /// </summary>
    Task<ICostCentreRegisterLock> LockRegisterAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The scope <see cref="ICostCentreRepository.LockRegisterAsync"/> returns. Committing releases
/// the lock together with the writes made under it. Disposing without committing rolls back and
/// releases it.
/// </summary>
public interface ICostCentreRegisterLock : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
