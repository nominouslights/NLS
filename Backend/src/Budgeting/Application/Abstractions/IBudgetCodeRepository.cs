using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>Write-side persistence for the BudgetCode aggregate (tenant-scoped).</summary>
public interface IBudgetCodeRepository
{
    Task<BudgetCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's code with that exact (already normalized) code string, or null. A targeted
    /// lookup rather than loading the whole chart: uniqueness here is one equality check, not a
    /// range intersection.
    /// </summary>
    Task<BudgetCode?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any of the tenant's codes roll up into this one. Both halves of the one-level
    /// hierarchy rule need it: an update must refuse to give a parent to a code that is itself a
    /// parent, and a delete must refuse to orphan children.
    /// </summary>
    Task<bool> HasChildrenAsync(Guid parentCodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's whole chart of accounts. The copy-from-an-earlier-period handler needs to ask
    /// "is this code still active?" once per source line, and one load beats an N+1 of
    /// <see cref="GetByIdAsync"/>. Here on the write side rather than on
    /// <c>IBudgetCodeReadService</c> for the same reason as <see cref="GetByCodeAsync"/>: the
    /// answer decides a write happening in this request, and the <c>rm_budget_codes</c>
    /// projection may still be a poll behind a retirement.
    /// </summary>
    Task<IReadOnlyList<BudgetCode>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(BudgetCode budgetCode);

    /// <summary>
    /// Hard delete — reserved for a code created in error that nothing has ever referenced. The
    /// caller is responsible for the usage and children guards; see the delete handler.
    /// </summary>
    void Remove(BudgetCode budgetCode);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
