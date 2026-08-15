using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Periods;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Read side — queries budgeting.rm_budget_periods and maps to the public contract.</summary>
internal sealed class BudgetPeriodReadService(BudgetingDbContext context) : IBudgetPeriodReadService
{
    public async Task<IReadOnlyList<BudgetPeriodResponse>> GetPeriodsAsync(
        CancellationToken cancellationToken = default)
    {
        var periods = await context.BudgetPeriodReadModels
            .AsNoTracking()
            .OrderBy(p => p.StartsOn)
            .ToListAsync(cancellationToken);

        return periods.Select(ToResponse).ToList();
    }

    private static BudgetPeriodResponse ToResponse(BudgetPeriodReadModel period) => new(
        period.Id,
        period.Label,
        period.Granularity,
        period.Year,
        period.Ordinal,
        period.StartsOn,
        period.EndsOn,
        period.State,
        period.CreatedAtUtc,
        period.UpdatedAtUtc);
}
