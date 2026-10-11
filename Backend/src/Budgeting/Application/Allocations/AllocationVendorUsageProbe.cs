using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// The real answer to "does anything reference this vendor": yes, while any budget item in any
/// period (closed ones and revenue-reclassified ones included) carries a vendor share for it. A
/// vendor that has been planned against is retired, never deleted — the
/// <see cref="AllocationBudgetCodeUsageProbe"/> rule, in Application for the same reason (pure
/// orchestration over a repository the tests already fake). The free-text payer on revenue items
/// is <em>not</em> consulted: it is a string somebody typed, not a reference to a register row.
/// Replaces <c>UnreferencedVendorUsageProbe</c>, which held the slot while nothing stored a vendor id.
/// </summary>
public sealed class AllocationVendorUsageProbe(IBudgetAllocationRepository allocations) : IVendorUsageProbe
{
    public Task<bool> IsReferencedAsync(Guid vendorId, CancellationToken cancellationToken = default) =>
        allocations.AnyWithVendorShareAsync(vendorId, cancellationToken);
}
