using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class BudgetCodeRepository(BudgetingDbContext context) : IBudgetCodeRepository
{
    public Task<BudgetCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // Ordinal equality on an already-normalized (upper-cased) string — the handler normalizes
    // before calling, so this is a plain index seek rather than a case-insensitive scan.
    public Task<BudgetCode?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

    // Served by the (tenant_id, parent_code_id) index. AnyAsync rather than a count: both callers
    // only need to know whether the set is empty.
    public Task<bool> HasChildrenAsync(Guid parentCodeId, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.AnyAsync(c => c.ParentCodeId == parentCodeId, cancellationToken);

    public void Add(BudgetCode budgetCode) => context.BudgetCodes.Add(budgetCode);

    public void Remove(BudgetCode budgetCode) => context.BudgetCodes.Remove(budgetCode);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
