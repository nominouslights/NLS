using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Actuals;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Read side for the plan-against-spend rollup. Three queries and a call to
/// <see cref="BudgetRollupBuilder"/> — this class does the fetching, the builder does the folding,
/// which is what makes "a code with spend and no plan still appears" a unit test instead of a
/// database fixture.
/// <para>
/// The join is done in memory rather than in SQL, the <see cref="BudgetPeriodReadService"/>
/// reasoning: the sets are small (a tenant's lines are at most periods × codes, and one period's
/// transactions smaller still), both sides of a full outer join contribute keys, and the SQL for
/// that would silently drop rows whose code row is missing while
/// <see cref="ActualTransactionReadService"/> still lists them. Doing every resolution the same
/// way is what keeps the rollup and the lines it is built from agreeing.
/// </para>
/// </summary>
internal sealed class BudgetRollupReadService(BudgetingDbContext context) : IBudgetRollupReadService
{
    public async Task<IReadOnlyList<BudgetRollupRowResponse>> GetForPeriodAsync(
        Guid periodId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        // The plan is addressed by period id; the spend by the period's inclusive dates, because
        // no transaction stores a period id. See ActualTransaction.
        var planned = await context.BudgetAllocationReadModels
            .AsNoTracking()
            .Where(a => a.PeriodId == periodId)
            .Select(a => new BudgetRollupBuilder.CodeAmount(a.BudgetCodeId, a.Code, a.AmountCad))
            .ToListAsync(cancellationToken);

        var actual = await context.ActualTransactionReadModels
            .AsNoTracking()
            .Where(t => t.OccurredOn >= startsOn && t.OccurredOn <= endsOn)
            .Select(t => new BudgetRollupBuilder.CodeAmount(t.BudgetCodeId, t.Code, t.AmountCad))
            .ToListAsync(cancellationToken);

        if (planned.Count == 0 && actual.Count == 0)
        {
            return [];
        }

        var codesById = await context.BudgetCodeReadModels
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.Category })
            .ToDictionaryAsync(
                c => c.Id,
                c => new BudgetRollupBuilder.CodeDescriptor(c.Name, c.Category),
                cancellationToken);

        return BudgetRollupBuilder.Build(planned, actual, codesById);
    }
}
