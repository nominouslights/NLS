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
}
