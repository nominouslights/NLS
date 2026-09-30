using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class BudgetAllocationRepository(BudgetingDbContext context) : IBudgetAllocationRepository
{
    // By primary key; the period predicate keeps a route naming one period from reaching an item
    // of another, and the tenant query filter (plus RLS) keeps it inside the tenant.
    public Task<BudgetAllocation?> GetByIdAsync(Guid periodId, Guid allocationId, CancellationToken cancellationToken = default) =>
        context.BudgetAllocations.FirstOrDefaultAsync(
            a => a.Id == allocationId && a.PeriodId == periodId, cancellationToken);

    // Over the write table, not the read model: the delete-code path asks this in the same
    // request that would remove the code, and the projection may still be a poll behind. Scoped
    // to the code's period (codes are per period, and the string repeats across periods); the
    // (tenant_id, period_id, budget_code_id) index narrows to the period first.
    public Task<bool> ExistsForCodeAsync(
        Guid periodId, Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        context.BudgetAllocations.AnyAsync(
            a => a.PeriodId == periodId && (a.BudgetCodeId == budgetCodeId || a.Code == code),
            cancellationToken);

    // Served by the leading (tenant_id, period_id) columns of the (tenant, period, code) index. Tracked, not
    // AsNoTracking: the copy handler calls CopyInto on the source lines, and the target lines it
    // reads back are the same entities a concurrent write in this unit of work would touch.
    public async Task<IReadOnlyList<BudgetAllocation>> ListForPeriodAsync(
        Guid periodId, CancellationToken cancellationToken = default) =>
        await context.BudgetAllocations
            .Where(a => a.PeriodId == periodId)
            .ToListAsync(cancellationToken);

    public void Add(BudgetAllocation allocation) => context.BudgetAllocations.Add(allocation);

    public void Remove(BudgetAllocation allocation) => context.BudgetAllocations.Remove(allocation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
