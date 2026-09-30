using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// In-memory fake of the write-side allocation repository for handler tests.
/// <para>
/// Every read is scoped to <see cref="QueryFilterTenantId"/>, modelling BudgetingDbContext's
/// tenant query filter (<c>a.TenantId == TenantId</c>, with RLS beneath it) — the
/// <see cref="InMemoryUserLookupRepository"/> precedent. Without it, a test seeding another
/// tenant's item would find it here while the real repository never could, and the handlers'
/// "another tenant's id reads as NotFound" contract would go untested.
/// </para>
/// </summary>
internal sealed class InMemoryBudgetAllocationRepository : IBudgetAllocationRepository
{
    public List<BudgetAllocation> Allocations { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>The ambient tenant the real DbContext's query filter would compare against.</summary>
    public Guid QueryFilterTenantId { get; set; } = TestBudgeting.TenantId;

    private IEnumerable<BudgetAllocation> Visible => Allocations.Where(a => a.TenantId == QueryFilterTenantId);

    public Task<BudgetAllocation?> GetByIdAsync(
        Guid periodId, Guid allocationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.FirstOrDefault(a => a.Id == allocationId && a.PeriodId == periodId));

    // Id OR string, like the real repository's `a.BudgetCodeId == budgetCodeId || a.Code == code`.
    // The string half is ordinal because a Postgres varchar equality is: a case-insensitive
    // match here would make the probe look more forgiving than the database it stands in for.
    public Task<bool> ExistsForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible.Any(a =>
            a.BudgetCodeId == budgetCodeId || string.Equals(a.Code, code, StringComparison.Ordinal)));

    // A copy of the list, like the real repository's ToListAsync: the copy handler iterates the
    // source items while adding to the repository, and a live view would be a mutation-during-
    // enumeration bug the real thing does not have.
    public Task<IReadOnlyList<BudgetAllocation>> ListForPeriodAsync(
        Guid periodId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BudgetAllocation>>(
            Visible.Where(a => a.PeriodId == periodId).ToList());

    public void Add(BudgetAllocation allocation) => Allocations.Add(allocation);

    public void Remove(BudgetAllocation allocation) => Allocations.Remove(allocation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
