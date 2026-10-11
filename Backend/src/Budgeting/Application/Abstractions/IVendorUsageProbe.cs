namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Answers the one question a vendor hard delete must ask: does anything reference this vendor?
/// A vendor that has been used is retired, never deleted — the <see cref="IBudgetCodeUsageProbe"/>
/// rule.
/// <para>
/// Budget items reference vendors through their vendor shares, so the shipped implementation
/// (<c>AllocationVendorUsageProbe</c>) answers "in use" while any item in any period carries a
/// share for the vendor. Actual transactions, when they arrive, are one more <c>||</c> there.
/// </para>
/// </summary>
public interface IVendorUsageProbe
{
    Task<bool> IsReferencedAsync(Guid vendorId, CancellationToken cancellationToken = default);
}
