using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Vendors;

/// <summary>
/// The register-only implementation of <see cref="IVendorUsageProbe"/>: nothing on the platform
/// carries a vendor id yet (budget items still hold a free-text vendor string), so no vendor can
/// be in use and every delete is allowed. <b>Temporary by design</b> — the next slice, which
/// links budget items to vendors, swaps this for a probe that checks those items (the
/// <c>AllocationBudgetCodeUsageProbe</c> pattern). Do not leave it registered once anything
/// stores a vendor id.
/// </summary>
public sealed class UnreferencedVendorUsageProbe : IVendorUsageProbe
{
    public Task<bool> IsReferencedAsync(Guid vendorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
