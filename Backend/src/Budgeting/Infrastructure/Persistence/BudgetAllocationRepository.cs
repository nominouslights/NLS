using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class BudgetAllocationRepository(BudgetingDbContext context) : IBudgetAllocationRepository
{
    // Served by the unique (tenant_id, period_id, budget_code_id) index.
    public Task<BudgetAllocation?> GetAsync(Guid periodId, Guid budgetCodeId, CancellationToken cancellationToken = default) =>
        context.BudgetAllocations.FirstOrDefaultAsync(
            a => a.PeriodId == periodId && a.BudgetCodeId == budgetCodeId, cancellationToken);

    // Over the write table, not the read model: the delete-code path asks this in the same
    // request that would remove the code, and the projection may still be a poll behind. Either
    // arm is a seek — (tenant_id, budget_code_id) and (tenant_id, code) both have an index.
    public Task<bool> ExistsForCodeAsync(Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        context.BudgetAllocations.AnyAsync(
            a => a.BudgetCodeId == budgetCodeId || a.Code == code, cancellationToken);

    // Same id-or-string match as ExistsForCodeAsync, and over the write table for the same
    // reason: the retirement check runs in the request that would retire the code, and the
    // projection may still be a poll behind.
    public async Task<IReadOnlyList<Guid>> GetPeriodIdsForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        await context.BudgetAllocations
            .Where(a => a.BudgetCodeId == budgetCodeId || a.Code == code)
            .Select(a => a.PeriodId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public void Add(BudgetAllocation allocation) => context.BudgetAllocations.Add(allocation);

    public void Remove(BudgetAllocation allocation) => context.BudgetAllocations.Remove(allocation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
