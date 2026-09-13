using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Tests;

/// <summary>In-memory fake of the write-side allocation repository for handler tests.</summary>
internal sealed class InMemoryBudgetAllocationRepository : IBudgetAllocationRepository
{
    public List<BudgetAllocation> Allocations { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<BudgetAllocation?> GetAsync(
        Guid periodId, Guid budgetCodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Allocations.FirstOrDefault(a => a.PeriodId == periodId && a.BudgetCodeId == budgetCodeId));

    // Id OR string, like the real repository's `a.BudgetCodeId == budgetCodeId || a.Code == code`.
    // The string half is ordinal because a Postgres varchar equality is: a case-insensitive
    // match here would make the probe look more forgiving than the database it stands in for.
    public Task<bool> ExistsForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Allocations.Any(a =>
            a.BudgetCodeId == budgetCodeId || string.Equals(a.Code, code, StringComparison.Ordinal)));

    public void Add(BudgetAllocation allocation) => Allocations.Add(allocation);

    public void Remove(BudgetAllocation allocation) => Allocations.Remove(allocation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
