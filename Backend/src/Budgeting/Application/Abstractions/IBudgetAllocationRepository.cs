using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>Write-side persistence for the BudgetAllocation aggregate (tenant-scoped).</summary>
public interface IBudgetAllocationRepository
{
    /// <summary>
    /// The tenant's line for that (period, code) pair, or null. Lines are keyed by the pair
    /// rather than by their own id everywhere a caller reaches them — the upsert-by-code rule
    /// means the pair <em>is</em> the identity a planner thinks in.
    /// </summary>
    Task<BudgetAllocation?> GetAsync(Guid periodId, Guid budgetCodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any line, in any period, references the code — by id <b>or</b> by string. Serves
    /// <see cref="IBudgetCodeUsageProbe"/>: a code deleted and recreated under the same string
    /// has a new id, and its old lines must still count as references.
    /// </summary>
    Task<bool> ExistsForCodeAsync(Guid budgetCodeId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every one of the tenant's lines in that period, in no guaranteed order. The copy handler's
    /// working set — it reads the source period's whole plan and the target's whole plan, then
    /// decides per line. Write-side rather than the <c>rm_</c> projection for the same reason
    /// <see cref="ExistsForCodeAsync"/> is: the copy writes in the same request, and the
    /// projection may still be a poll behind.
    /// </summary>
    Task<IReadOnlyList<BudgetAllocation>> ListForPeriodAsync(
        Guid periodId, CancellationToken cancellationToken = default);

    void Add(BudgetAllocation allocation);

    /// <summary>Hard delete — the line is a plan entry, not history; the journal keeps its final snapshot.</summary>
    void Remove(BudgetAllocation allocation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
