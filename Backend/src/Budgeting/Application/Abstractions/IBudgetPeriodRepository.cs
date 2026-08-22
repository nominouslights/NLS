using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>Write-side persistence for the BudgetPeriod aggregate (tenant-scoped).</summary>
public interface IBudgetPeriodRepository
{
    Task<BudgetPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All of the current tenant's periods — the overlap check's working set.</summary>
    Task<IReadOnlyList<BudgetPeriod>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(BudgetPeriod period);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
