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
    /// The distinct periods that have planned against the code — by id <b>or</b> by string, the
    /// same match <see cref="ExistsForCodeAsync"/> makes, so "which rows belong to this code" has
    /// one answer across the module rather than two.
    /// <para>
    /// Serves the retirement rule, which needs to know <em>which</em> periods reference the code
    /// and what state each is in. <see cref="IBudgetCodeUsageProbe"/> deliberately cannot answer
    /// that — it returns a bool, and widening it to carry period state would make "is this code
    /// referenced" mean two different things at its two call sites.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> GetPeriodIdsForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default);

    void Add(BudgetAllocation allocation);

    /// <summary>Hard delete — the line is a plan entry, not history; the journal keeps its final snapshot.</summary>
    void Remove(BudgetAllocation allocation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
