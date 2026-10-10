using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// In-memory fake of the write-side code repository for handler tests. Every lookup carries the
/// period predicate, like the real repository, so "another period's code reads as not found" is
/// exercised rather than assumed; and every read is scoped to <see cref="QueryFilterTenantId"/>,
/// modelling the tenant query filter (the <see cref="InMemoryBudgetAllocationRepository"/> precedent).
/// </summary>
internal sealed class InMemoryBudgetCodeRepository : IBudgetCodeRepository
{
    public List<BudgetCode> Codes { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>The ambient tenant the real DbContext's query filter would compare against.</summary>
    public Guid QueryFilterTenantId { get; set; } = TestBudgeting.TenantId;

    private IEnumerable<BudgetCode> Visible => Codes.Where(c => c.TenantId == QueryFilterTenantId);

    public Task<BudgetCode?> GetByIdAsync(Guid periodId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(c => c.Id == id && c.PeriodId == periodId));

    // Ordinal, matching the real repository: the handler normalizes before calling, so a
    // case-insensitive comparison here would hide a missing normalization rather than paper over it.
    public Task<BudgetCode?> GetByCodeAsync(
        Guid periodId, string normalizedCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(c =>
            c.PeriodId == periodId && string.Equals(c.Code, normalizedCode, StringComparison.Ordinal)));

    public Task<bool> HasChildrenAsync(Guid periodId, Guid parentCodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.Any(c => c.PeriodId == periodId && c.ParentCodeId == parentCodeId));

    // A copy of the list, like the real repository's ToListAsync: the copy handler adds to the
    // repository while it works through the chart it read.
    public Task<IReadOnlyList<BudgetCode>> ListForPeriodAsync(Guid periodId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BudgetCode>>(Visible.Where(c => c.PeriodId == periodId).ToList());

    // Every period, ordinal — matching the real repository's cross-period cost-centre probe.
    public Task<bool> AnyWithCostCentreAsync(string costCentreCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.Any(c => string.Equals(c.CostCentre, costCentreCode, StringComparison.Ordinal)));

    public void Add(BudgetCode budgetCode) => Codes.Add(budgetCode);

    public void Remove(BudgetCode budgetCode) => Codes.Remove(budgetCode);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
