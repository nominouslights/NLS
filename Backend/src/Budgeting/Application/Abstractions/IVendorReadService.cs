using NorthernLink.Budgeting.Application.Vendors;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_vendors. No tenant parameter — the DbContext's tenant query filter
/// (and RLS underneath it) scopes every query to the ambient tenant.
/// </summary>
public interface IVendorReadService
{
    /// <summary>The tenant's register ordered by name; retired vendors only when asked for.</summary>
    Task<IReadOnlyList<VendorResponse>> GetVendorsAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>One vendor, or null (including another tenant's).</summary>
    Task<VendorResponse?> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One period's planned expense per vendor (see <see cref="VendorPlannedRollup"/>), read from
    /// the same projections the period totals read. The caller has already proved the period exists.
    /// </summary>
    Task<VendorRollupResponse> GetPlannedRollupAsync(Guid periodId, CancellationToken cancellationToken = default);
}
