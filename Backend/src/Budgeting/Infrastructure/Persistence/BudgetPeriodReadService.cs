using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Periods;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side — queries budgeting.rm_budget_periods and maps to the public contract, with each
/// period's planned revenue and planned expense summed from its allocation lines.
/// <para>
/// <b>The totals are grouped in memory, not in SQL.</b> The lines are pulled as bare
/// <c>(PeriodId, BudgetCodeId, AmountCad)</c> triples and bucketed against the chart's
/// id → category dictionary — the same two-dictionaries shape as <see cref="BudgetCodeReadService"/>,
/// and for the same reason: the sets are small (a tenant's lines are at most periods × codes),
/// and the alternative — a <c>GROUP BY</c> over an inner join to <c>rm_budget_codes</c> — would
/// silently drop any line whose code row is missing, while <see cref="BudgetAllocationReadService"/>
/// still lists that line as Expense. Doing both resolutions the same way is what keeps a
/// period's totals and its lines agreeing. A line whose code is unknown counts as Expense,
/// matching the lines read.
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

        var totals = await LoadTotalsAsync(context.BudgetAllocationReadModels, cancellationToken);

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
            context.BudgetAllocationReadModels.Where(a => a.PeriodId == periodId), cancellationToken);

        return ToResponse(period, totals);
    }

    /// <summary>
    /// Sums the given lines into (revenue, expense) per period, resolving each line's category
    /// through the chart. Shared by the list and the single read so the two can never disagree
    /// on how a total is built.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, PlannedTotals>> LoadTotalsAsync(
        IQueryable<BudgetAllocationReadModel> lines,
        CancellationToken cancellationToken)
    {
        var amounts = await lines
            .AsNoTracking()
            .Select(a => new { a.PeriodId, a.BudgetCodeId, a.AmountCad })
            .ToListAsync(cancellationToken);

        if (amounts.Count == 0)
        {
            return new Dictionary<Guid, PlannedTotals>();
        }

        var categoryByCodeId = await context.BudgetCodeReadModels
            .AsNoTracking()
            .Select(c => new { c.Id, c.Category })
            .ToDictionaryAsync(c => c.Id, c => c.Category, cancellationToken);

        var totals = new Dictionary<Guid, PlannedTotals>();
        foreach (var amount in amounts)
        {
            var isRevenue = categoryByCodeId.TryGetValue(amount.BudgetCodeId, out var category)
                && category == nameof(BudgetCodeCategory.Revenue);

            var current = totals.GetValueOrDefault(amount.PeriodId, PlannedTotals.Zero);
            totals[amount.PeriodId] = isRevenue
                ? current with { RevenueCad = current.RevenueCad + amount.AmountCad }
                : current with { ExpenseCad = current.ExpenseCad + amount.AmountCad };
        }

        return totals;
    }

    private static BudgetPeriodResponse ToResponse(
        BudgetPeriodReadModel period,
        IReadOnlyDictionary<Guid, PlannedTotals> totals)
    {
        var planned = totals.GetValueOrDefault(period.Id, PlannedTotals.Zero);

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

    private sealed record PlannedTotals(decimal RevenueCad, decimal ExpenseCad)
    {
        public static readonly PlannedTotals Zero = new(0m, 0m);
    }
}
