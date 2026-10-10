namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Answers the one question a vendor hard delete must ask: does anything reference this vendor?
/// A vendor that has been used is retired, never deleted — the <see cref="IBudgetCodeUsageProbe"/>
/// rule.
/// <para>
/// <b>Today nothing can reference a vendor</b>, so the shipped implementation
/// (<c>UnreferencedVendorUsageProbe</c>) always answers "not in use". The interface exists now so
/// the delete handler and its 409 <c>Budgeting.Vendor.InUse</c> path are built and tested once:
/// the slice that links budget items to vendors replaces the implementation with one that checks
/// items, without touching this interface or the handler.
/// </para>
/// </summary>
public interface IVendorUsageProbe
{
    Task<bool> IsReferencedAsync(Guid vendorId, CancellationToken cancellationToken = default);
}
