using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class BudgetPeriodRepository(BudgetingDbContext context) : IBudgetPeriodRepository
{
    public Task<BudgetPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.BudgetPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BudgetPeriod>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.BudgetPeriods.ToListAsync(cancellationToken);

    public void Add(BudgetPeriod period) => context.BudgetPeriods.Add(period);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
