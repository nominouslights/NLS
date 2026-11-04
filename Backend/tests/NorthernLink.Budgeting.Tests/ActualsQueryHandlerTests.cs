using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Actuals;
using NorthernLink.Budgeting.Application.Actuals.GetActuals;
using NorthernLink.Budgeting.Application.Actuals.GetRollup;
using NorthernLink.Budgeting.Application.Periods;
using NorthernLink.Budgeting.Domain.Periods;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The two period-scoped reads. Both do the same two things and both matter: a wrong period id is
/// a 404 rather than an empty list — "no such period" and "a period nobody has spent against" look
/// identical on a dashboard otherwise — and the period's inclusive dates are what get handed to
/// the read side, because no transaction stores a period id.
/// </summary>
public class ActualsQueryHandlerTests
{
    private static readonly Guid PeriodId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static BudgetPeriodResponse Q4() => new(
        PeriodId, "FY2026 Q4", nameof(PeriodGranularity.Quarter), 2026, 4,
        new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), nameof(PeriodState.Open),
        0m, 0m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    // --- GET /periods/{id}/actuals ---

    [Fact]
    public async Task An_unknown_period_reports_the_period_as_not_found_rather_than_an_empty_list()
    {
        var handler = new GetActualTransactionsQueryHandler(new StubActuals(), new StubPeriods(null));

        var result = await handler.Handle(
            new GetActualTransactionsQuery(TestBudgeting.TenantId, PeriodId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task The_actuals_read_is_handed_the_periods_inclusive_dates()
    {
        var actuals = new StubActuals();
        var handler = new GetActualTransactionsQueryHandler(actuals, new StubPeriods(Q4()));

        var result = await handler.Handle(
            new GetActualTransactionsQuery(TestBudgeting.TenantId, PeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 10, 1), actuals.StartsOn);
        Assert.Equal(new DateOnly(2026, 12, 31), actuals.EndsOn);
    }

    [Fact]
    public async Task A_real_period_with_no_transactions_is_an_empty_list_not_a_failure()
    {
        var handler = new GetActualTransactionsQueryHandler(new StubActuals(), new StubPeriods(Q4()));

        var result = await handler.Handle(
            new GetActualTransactionsQuery(TestBudgeting.TenantId, PeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    // --- GET /periods/{id}/rollup ---

    [Fact]
    public async Task An_unknown_period_has_no_rollup_either()
    {
        var handler = new GetBudgetRollupQueryHandler(new StubRollup(), new StubPeriods(null));

        var result = await handler.Handle(
            new GetBudgetRollupQuery(TestBudgeting.TenantId, PeriodId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task The_rollup_read_is_handed_both_the_period_id_and_its_dates()
    {
        // Both halves of the rollup's addressing: the plan comes from the id, the spend from the
        // dates, because no transaction stores a period id.
        var rollup = new StubRollup();
        var handler = new GetBudgetRollupQueryHandler(rollup, new StubPeriods(Q4()));

        var result = await handler.Handle(
            new GetBudgetRollupQuery(TestBudgeting.TenantId, PeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PeriodId, rollup.PeriodId);
        Assert.Equal(new DateOnly(2026, 10, 1), rollup.StartsOn);
        Assert.Equal(new DateOnly(2026, 12, 31), rollup.EndsOn);
    }

    private sealed class StubPeriods(BudgetPeriodResponse? period) : IBudgetPeriodReadService
    {
        public Task<IReadOnlyList<BudgetPeriodResponse>> GetPeriodsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BudgetPeriodResponse>>([]);

        public Task<BudgetPeriodResponse?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default) =>
            Task.FromResult(period);
    }

    private sealed class StubActuals : IActualTransactionReadService
    {
        public DateOnly? StartsOn { get; private set; }

        public DateOnly? EndsOn { get; private set; }

        public Task<IReadOnlyList<ActualTransactionResponse>> GetForDateRangeAsync(
            DateOnly startsOn, DateOnly endsOn, CancellationToken cancellationToken = default)
        {
            StartsOn = startsOn;
            EndsOn = endsOn;
            return Task.FromResult<IReadOnlyList<ActualTransactionResponse>>([]);
        }
    }

    private sealed class StubRollup : IBudgetRollupReadService
    {
        public Guid? PeriodId { get; private set; }

        public DateOnly? StartsOn { get; private set; }

        public DateOnly? EndsOn { get; private set; }

        public Task<IReadOnlyList<BudgetRollupRowResponse>> GetForPeriodAsync(
            Guid periodId, DateOnly startsOn, DateOnly endsOn, CancellationToken cancellationToken = default)
        {
            PeriodId = periodId;
            StartsOn = startsOn;
            EndsOn = endsOn;
            return Task.FromResult<IReadOnlyList<BudgetRollupRowResponse>>([]);
        }
    }
}
