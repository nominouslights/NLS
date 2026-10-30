using Microsoft.EntityFrameworkCore;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>A register vendor as the read side shows it beside a share: its current name and active flag.</summary>
internal readonly record struct ResolvedVendor(string Name, bool IsActive);

/// <summary>
/// The one way the read side turns vendor-share ids into names — shared by the item list
/// (<see cref="BudgetAllocationReadService"/>) and the by-vendor rollup
/// (<see cref="VendorReadService"/>), so the two can never resolve a vendor differently. Resolved
/// from rm_vendors at read time, never snapshotted onto the share, so a renamed or retired vendor
/// reads right everywhere. Tenant-filtered (query filter + RLS) like every rm_* read.
/// </summary>
internal static class VendorResolution
{
    private static readonly IReadOnlyDictionary<Guid, ResolvedVendor> None = new Dictionary<Guid, ResolvedVendor>();

    /// <summary>
    /// The named vendors that exist in the register, keyed by id. An id with no register row is
    /// simply absent (the caller decides how to show it). No ids, no query.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ResolvedVendor>> ResolveVendorsAsync(
        this BudgetingDbContext context, IEnumerable<Guid> vendorIds, CancellationToken cancellationToken)
    {
        var ids = vendorIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return None;
        }

        return await context.VendorReadModels
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.Name, v.IsActive })
            .ToDictionaryAsync(v => v.Id, v => new ResolvedVendor(v.Name, v.IsActive), cancellationToken);
    }
}
