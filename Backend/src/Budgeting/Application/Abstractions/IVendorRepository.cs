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
}
