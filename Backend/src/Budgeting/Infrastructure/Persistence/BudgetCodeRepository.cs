using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered). Every query
/// carries the period predicate, so a code of another period is invisible here exactly as a code
/// of another tenant is.
/// </summary>
internal sealed class BudgetCodeRepository(BudgetingDbContext context) : IBudgetCodeRepository
{
    public Task<BudgetCode?> GetByIdAsync(Guid periodId, Guid id, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.FirstOrDefaultAsync(c => c.Id == id && c.PeriodId == periodId, cancellationToken);

    // Ordinal equality on an already-normalized (upper-cased) string — the handler normalizes
    // before calling, so this is a seek on the unique (tenant_id, period_id, code) index rather
    // than a case-insensitive scan.
    public Task<BudgetCode?> GetByCodeAsync(
        Guid periodId, string normalizedCode, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.FirstOrDefaultAsync(
            c => c.PeriodId == periodId && c.Code == normalizedCode, cancellationToken);

    // Served by the (tenant_id, parent_code_id) index. AnyAsync rather than a count: both callers
    // only need to know whether the set is empty.
    public Task<bool> HasChildrenAsync(Guid periodId, Guid parentCodeId, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.AnyAsync(
            c => c.PeriodId == periodId && c.ParentCodeId == parentCodeId, cancellationToken);

    // One period's chart in one query — a dozen-odd rows, and the copy handlers would otherwise
    // issue one lookup per row.
    public async Task<IReadOnlyList<BudgetCode>> ListForPeriodAsync(
        Guid periodId, CancellationToken cancellationToken = default) =>
        await context.BudgetCodes.Where(c => c.PeriodId == periodId).ToListAsync(cancellationToken);

    // Every period, on purpose — see the interface. Served by the (tenant_id, cost_centre) index.
    public Task<bool> AnyWithCostCentreAsync(string costCentreCode, CancellationToken cancellationToken = default) =>
        context.BudgetCodes.AnyAsync(c => c.CostCentre == costCentreCode, cancellationToken);

    public void Add(BudgetCode budgetCode) => context.BudgetCodes.Add(budgetCode);

    public void Remove(BudgetCode budgetCode) => context.BudgetCodes.Remove(budgetCode);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
