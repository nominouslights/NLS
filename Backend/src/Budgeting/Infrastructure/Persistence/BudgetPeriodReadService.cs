using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Periods;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_budget_periods and maps to the public contract, with each
/// period's planned revenue and planned expense summed from its budget items.
/// <para>
/// <b>The totals are computed in memory, by <see cref="PeriodPlannedTotals"/>.</b> The items are
/// pulled as bare <c>(PeriodId, BudgetCodeId, AmountCad)</c> triples and classified against the
/// chart's (period, code id) → category entries — each item by <em>its own period's</em> code. A
/// <c>GROUP BY</c> over an inner join to <c>rm_budget_codes</c> would silently drop any item
/// whose code row is missing, while <see cref="BudgetAllocationReadService"/> still lists that
/// item as Expense; doing both resolutions the same way is what keeps a period's totals and its
/// items agreeing.
/// </para>
/// </summary>
internal sealed class BudgetPeriodReadService(BudgetingDbContext context) : IBudgetPeriodReadService
{
    public async Task<IReadOnlyList<BudgetPeriodResponse>> GetPeriodsAsync(
        CancellationToken cancellationToken = default)
    {
        var periods = await context.BudgetPeriodReadModels
            .AsNoTracking()
            .OrderBy(p => p.StartsOn)
            .ToListAsync(cancellationToken);

        if (periods.Count == 0)
        {
            return [];
        }

        var totals = await LoadTotalsAsync(
            context.BudgetAllocationReadModels, context.BudgetCodeReadModels, cancellationToken);

        return periods.Select(period => ToResponse(period, totals)).ToList();
    }

    public async Task<BudgetPeriodResponse?> GetPeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var period = await context.BudgetPeriodReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period is null)
        {
            return null;
        }

        var totals = await LoadTotalsAsync(
            context.BudgetAllocationReadModels.Where(a => a.PeriodId == periodId),
            context.BudgetCodeReadModels.Where(c => c.PeriodId == periodId),
            cancellationToken);

        return ToResponse(period, totals);
    }

    /// <summary>
    /// Loads the given items and codes and sums them per period. Shared by the list and the single
    /// read so the two can never disagree on how a total is built.
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, PeriodPlannedTotals.Totals>> LoadTotalsAsync(
        IQueryable<BudgetAllocationReadModel> items,
        IQueryable<BudgetCodeReadModel> codes,
        CancellationToken cancellationToken)
    {
        var amounts = await items
            .AsNoTracking()
            .Select(a => new PeriodPlannedTotals.ItemAmount(a.PeriodId, a.BudgetCodeId, a.AmountCad))
            .ToListAsync(cancellationToken);

        if (amounts.Count == 0)
        {
            return new Dictionary<Guid, PeriodPlannedTotals.Totals>();
        }

        var categories = await codes
            .AsNoTracking()
            .Select(c => new PeriodPlannedTotals.CodeCategory(c.PeriodId, c.Id, c.Category))
            .ToListAsync(cancellationToken);

        return PeriodPlannedTotals.Sum(amounts, categories);
    }

    private static BudgetPeriodResponse ToResponse(
        BudgetPeriodReadModel period,
        IReadOnlyDictionary<Guid, PeriodPlannedTotals.Totals> totals)
    {
        var planned = totals.GetValueOrDefault(period.Id, PeriodPlannedTotals.Totals.Zero);

        return new BudgetPeriodResponse(
            period.Id,
            period.Label,
            period.Granularity,
            period.Year,
            period.Ordinal,
            period.StartsOn,
            period.EndsOn,
            period.State,
            planned.RevenueCad,
            planned.ExpenseCad,
            period.CreatedAtUtc,
            period.UpdatedAtUtc);
    }
}
