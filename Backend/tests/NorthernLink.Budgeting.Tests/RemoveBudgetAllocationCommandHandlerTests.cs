using NorthernLink.Budgeting.Application.Allocations.Remove;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// RemoveBudgetAllocationCommandHandler: period guards first (exists, editable), then the line
/// must exist. Retired codes are deliberately not a guard — cleaning a retired code out of a plan
/// is the point. Every refusal asserts the row is still there and nothing was saved.
/// </summary>
public class RemoveBudgetAllocationCommandHandlerTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly RemoveBudgetAllocationCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _code = TestBudgeting.CreateCode("ZBB-CREW-01");

    public RemoveBudgetAllocationCommandHandlerTests()
    {
        _handler = new RemoveBudgetAllocationCommandHandler(_allocations, _periods);
        _periods.Add(_draft);
    }

    private Task<Result> RemoveAsync(Guid? periodId = null, Guid? codeId = null) =>
        _handler.Handle(
            new RemoveBudgetAllocationCommand(TestBudgeting.TenantId, periodId ?? _draft.Id, codeId ?? _code.Id),
            CancellationToken.None);

    private BudgetAllocation AddLine(BudgetPeriod? period = null, BudgetCode? code = null)
    {
        period ??= _draft;
        code ??= _code;
        var line = TestBudgeting.CreateAllocation(period.Id, code.Id, code.Code);
        _allocations.Add(line);
        return line;
    }

    [Fact]
    public async Task The_line_is_removed_and_saved_once_leaving_other_lines_alone()
    {
        AddLine();
        var fuel = TestBudgeting.CreateCode("ZBB-FUEL-01");
        var fuelLine = AddLine(code: fuel);

        var result = await RemoveAsync(codeId: _code.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(fuelLine, Assert.Single(_allocations.Allocations));
        Assert.Equal(1, _allocations.SaveChangesCallCount);
        Assert.Equal(0, _periods.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_code_with_no_line_in_this_period_reports_the_allocation_as_not_found()
    {
        var result = await RemoveAsync(codeId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_line_in_another_period_is_not_reachable_from_this_one()
    {
        // The pair (period, code) is the identity: the same code planned in Q1 2027 is not the
        // line the caller asked to remove from Q4 2026.
        var other = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(other);
        AddLine(period: other);

        var result = await RemoveAsync(periodId: _draft.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_period_reports_the_period_as_not_found()
    {
        AddLine();

        var result = await RemoveAsync(periodId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_read_only_period_keeps_its_lines(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);
        AddLine(period: period);

        var result = await RemoveAsync(periodId: period.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_line_lookup()
    {
        // Closed period, no line: the period is the answer, not NotFound.
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed);
        _periods.Add(closed);

        var result = await RemoveAsync(periodId: closed.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
    }

    [Fact]
    public async Task An_open_period_allows_removal()
    {
        var open = TestBudgeting.PeriodIn(PeriodState.Open);
        _periods.Add(open);
        AddLine(period: open);

        var result = await RemoveAsync(periodId: open.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_line_whose_code_has_since_been_retired_can_still_be_removed()
    {
        // Deliberate: removal is not gated on IsActive. The set handler refuses new plan against a
        // retired code; taking the old plan off it is exactly the clean-up a planner needs.
        AddLine();
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await RemoveAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }
}
