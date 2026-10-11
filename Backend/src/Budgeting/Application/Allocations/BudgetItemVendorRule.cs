using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// A budget item's vendor shares must name vendors of the tenant's register — shared by create
/// and update for the <c>BudgetCodeCostCentreRule</c> reason, and living here rather than on the
/// aggregate because it needs the register, which an aggregate cannot query.
/// <list type="bullet">
/// <item>Only vendors the write <b>adds</b> are looked up (<see cref="BudgetAllocation.AddedVendorIds"/>).
/// A share the item already had passes without a lookup, so retiring a vendor never freezes the
/// items already planned against it — the "only on change" leniency.</item>
/// <item>No vendor with that id in this tenant → <see cref="BudgetAllocationErrors.VendorNotFound"/> (400).</item>
/// <item>A retired vendor → <see cref="BudgetAllocationErrors.VendorInactive"/> (409).</item>
/// </list>
/// <para>
/// <b>Values the aggregate itself will refuse are left to it</b>: shares on a revenue item
/// (<see cref="BudgetAllocationErrors.VendorSharesNotAllowedForRevenue"/>) skip the register
/// entirely, so the planner hears about the category, not about a vendor they should not have
/// picked at all. (Shape errors — a bad amount, a duplicate, a wrong sum — never get this far:
/// the handlers run <see cref="BudgetAllocation.Validate"/> first.)
/// </para>
/// <para>
/// <b>Callers lock first.</b> A lookup that passes can still be followed by a vendor delete before
/// the item commits, so callers take <see cref="LockIfAddingAsync"/> before
/// <see cref="ValidateAsync"/> and commit it after their save. The vendor delete probes usage
/// under the same lock.
/// </para>
/// </summary>
public static class BudgetItemVendorRule
{
    /// <param name="current">The item's shares before this write: empty on create.</param>
    public static async Task<Result> ValidateAsync(
        IVendorRepository vendors,
        BudgetItemDetails details,
        IReadOnlyCollection<BudgetItemVendorShare> current,
        BudgetCodeCategory codeCategory,
        CancellationToken cancellationToken)
    {
        foreach (var vendorId in RegisterCheckFor(details, current, codeCategory))
        {
            var vendor = await vendors.GetByIdAsync(vendorId, cancellationToken);
            if (vendor is null)
            {
                return Result.Failure(BudgetAllocationErrors.VendorNotFound);
            }

            if (!vendor.IsActive)
            {
                return Result.Failure(BudgetAllocationErrors.VendorInactive(vendor.Name));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Takes the tenant's vendor register lock when, and only when, <see cref="ValidateAsync"/>
    /// would look a vendor up — that is, when the write adds a vendor to the item. Returns null
    /// when there is nothing to lock. The caller holds the scope across the lookup and its save,
    /// then commits it.
    /// </summary>
    public static async Task<IVendorRegisterLock?> LockIfAddingAsync(
        IVendorRepository vendors,
        Guid tenantId,
        BudgetItemDetails details,
        IReadOnlyCollection<BudgetItemVendorShare> current,
        BudgetCodeCategory codeCategory,
        CancellationToken cancellationToken) =>
        RegisterCheckFor(details, current, codeCategory).Count > 0
            ? await vendors.LockRegisterAsync(tenantId, cancellationToken)
            : null;

    private static IReadOnlyList<Guid> RegisterCheckFor(
        BudgetItemDetails details,
        IReadOnlyCollection<BudgetItemVendorShare> current,
        BudgetCodeCategory codeCategory) =>
        codeCategory == BudgetCodeCategory.Revenue ? [] : BudgetAllocation.AddedVendorIds(details, current);
}
