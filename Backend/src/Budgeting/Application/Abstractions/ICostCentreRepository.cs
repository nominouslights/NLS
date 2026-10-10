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

    /// <summary>Hard delete — the caller runs the children and usage guards first.</summary>
    void Remove(CostCentre costCentre);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves like <see cref="SaveChangesAsync"/>, but returns false (it does not throw) when
    /// the save lost a race on the unique (tenant_id, code) index: another request created the
    /// same code between the handler's <see cref="GetByCodeAsync"/> check and this commit. On false
    /// nothing was persisted and the unit of work has been cleared, so the caller can re-read the
    /// winner and report the normal 409. Any other failure still throws. The
    /// <see cref="IVendorRepository.TrySaveChangesAsync"/> shape.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes every write that decides whether a cost-centre code is in use: the delete
    /// handler's usage probe and hard delete on one side, and a budget-code create/edit that
    /// starts carrying the code on the other. Opens a transaction on this unit of work (unless one
    /// is already open) and takes a transaction-scoped lock keyed on (tenant, code). The lock is
    /// held until the returned scope commits, or is disposed without committing (rollback), so the
    /// caller must read, write and <see cref="ICostCentreCodeLock.CommitAsync"/> inside it.
    /// </summary>
    Task<ICostCentreCodeLock> LockCodeAsync(
        Guid tenantId, string normalizedCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// The scope <see cref="ICostCentreRepository.LockCodeAsync"/> returns. Committing releases the
/// lock together with the writes made under it. Disposing without committing rolls back and
/// releases it.
/// </summary>
public interface ICostCentreCodeLock : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
