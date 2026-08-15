using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Tests;

/// <summary>In-memory fake of the write-side period repository for handler tests.</summary>
internal sealed class InMemoryBudgetPeriodRepository : IBudgetPeriodRepository
{
    public List<BudgetPeriod> Periods { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<BudgetPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Periods.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<BudgetPeriod>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BudgetPeriod>>(Periods.ToList());

    public void Add(BudgetPeriod period) => Periods.Add(period);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
