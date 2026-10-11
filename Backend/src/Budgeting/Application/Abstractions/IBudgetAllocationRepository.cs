using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>Write-side persistence for the BudgetAllocation aggregate (tenant-scoped).</summary>
public interface IBudgetAllocationRepository
{
    /// <summary>
    /// The tenant's budget item with that id <b>in that period</b>, or null. The period is part of
    /// the lookup so a route naming one period can never reach an item of another; the tenant
    /// query filter (and RLS beneath it) means another tenant's item id is null here too.
    /// </summary>
    Task<BudgetAllocation?> GetByIdAsync(Guid periodId, Guid allocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any item <b>in that period</b> references the code — by id <b>or</b> by string.
    /// Serves <see cref="IBudgetCodeUsageProbe"/>: a code deleted and recreated under the same
    /// string has a new id, and its old items must still count as references. Period-scoped
    /// because codes are: another period's items reference another period's code of that string.
    /// </summary>
    Task<bool> ExistsForCodeAsync(Guid periodId, Guid budgetCodeId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every one of the tenant's lines in that period, in no guaranteed order. The copy handler's
    /// working set — it reads the source period's whole plan and the target's whole plan, then
    /// decides per line. Write-side rather than the <c>rm_</c> projection for the same reason
    /// <see cref="ExistsForCodeAsync"/> is: the copy writes in the same request, and the
    /// projection may still be a poll behind.
    /// </summary>
    Task<IReadOnlyList<BudgetAllocation>> ListForPeriodAsync(
        Guid periodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any of the tenant's items, <b>in any period</b> (closed ones included — a closed
    /// plan still explains its numbers), carries a vendor share for this vendor. Serves
    /// <see cref="IVendorUsageProbe"/>. Over the write table, for the
    /// <see cref="ExistsForCodeAsync"/> reason: the vendor delete asks in the same request that
    /// would remove the vendor, and the projection may be a poll behind.
    /// </summary>
    Task<bool> AnyWithVendorShareAsync(Guid vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any of the tenant's items <b>in that period</b> carries at least one vendor share —
    /// a scalar query that tracks nothing. The copy handler asks it before its tracked read of the
    /// source plan, to take the vendor register lock only when the copy might carry shares.
    /// </summary>
    Task<bool> AnyWithVendorSharesInPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);

    void Add(BudgetAllocation allocation);

    /// <summary>Hard delete — the line is a plan entry, not history; the journal keeps its final snapshot.</summary>
    void Remove(BudgetAllocation allocation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
