using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Write-side persistence for the BudgetCode aggregate (tenant-scoped).
/// <para>
/// <b>Every lookup names the period.</b> A budget code belongs to one period's chart, so a code id
/// from another period — or another tenant, through the query filter and RLS beneath it — reads
/// back as null and reports not-found. That is how "an item's code must belong to the item's
/// period" and "a parent must be in the same period" are enforced without a separate comparison
/// anywhere a reviewer could forget one.
/// </para>
/// </summary>
public interface IBudgetCodeRepository
{
    /// <summary>The period's code with that id, or null (including when the id is another period's code).</summary>
    Task<BudgetCode?> GetByIdAsync(Guid periodId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The period's code with that exact (already normalized) code string, or null. A targeted
    /// lookup rather than loading the whole chart: uniqueness here is one equality check.
    /// </summary>
    Task<BudgetCode?> GetByCodeAsync(Guid periodId, string normalizedCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any of the period's codes roll up into this one. Both halves of the one-level
    /// hierarchy rule need it: an update must refuse to give a parent to a code that is itself a
    /// parent, and a delete must refuse to orphan children.
    /// </summary>
    Task<bool> HasChildrenAsync(Guid periodId, Guid parentCodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The period's whole chart, retired codes included. The two copy handlers (codes and items)
    /// read a source and a target chart once each rather than issuing one lookup per row. Here on
    /// the write side rather than on <c>IBudgetCodeReadService</c> because the answer decides a
    /// write happening in this request, and the <c>rm_budget_codes</c> projection may still be a
    /// poll behind.
    /// </summary>
    Task<IReadOnlyList<BudgetCode>> ListForPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);

    void Add(BudgetCode budgetCode);

    /// <summary>
    /// Hard delete — reserved for a code created in error that nothing has ever referenced. The
    /// caller is responsible for the usage and children guards; see the delete handler.
    /// </summary>
    void Remove(BudgetCode budgetCode);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
