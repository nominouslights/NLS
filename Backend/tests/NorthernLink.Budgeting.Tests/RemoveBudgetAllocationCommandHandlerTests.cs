using NorthernLink.Budgeting.Application.Allocations.Remove;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// RemoveBudgetAllocationCommandHandler: delete by item id. Period guards first (exists,
/// editable), then the item must exist in that period and tenant. Retired codes are deliberately
/// not a guard — cleaning a retired code out of a plan is the point. Every refusal asserts the
/// row is still there and nothing was saved.
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

    private Task<Result> RemoveAsync(Guid allocationId, Guid? periodId = null) =>
        _handler.Handle(
            new RemoveBudgetAllocationCommand(TestBudgeting.TenantId, periodId ?? _draft.Id, allocationId),
            CancellationToken.None);

    private BudgetAllocation AddLine(BudgetPeriod? period = null, BudgetCode? code = null, Guid? tenantId = null)
    {
        period ??= _draft;
        code ??= _code;
        var line = TestBudgeting.CreateAllocation(period.Id, code.Id, code.Code, tenantId: tenantId);
        _allocations.Add(line);
        return line;
    }

    [Fact]
    public async Task The_item_is_removed_by_id_and_saved_once_leaving_its_siblings_on_the_same_code_alone()
    {
        var target = AddLine();
        var sibling = AddLine();
        var fuelLine = AddLine(code: TestBudgeting.CreateCode("ZBB-FUEL-01"));

        var result = await RemoveAsync(target.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _allocations.Allocations.Count);
        Assert.Contains(sibling, _allocations.Allocations);
        Assert.Contains(fuelLine, _allocations.Allocations);
        Assert.DoesNotContain(target, _allocations.Allocations);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
        Assert.Equal(0, _periods.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_item_id_reports_the_item_as_not_found()
    {
        AddLine();

        var result = await RemoveAsync(Guid.NewGuid());

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_code_id_is_not_an_item_id()
    {
        // The old route addressed items by code; a stale client sending a code id must miss.
        AddLine();

        var result = await RemoveAsync(_code.Id);

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Single(_allocations.Allocations);
    }

    [Fact]
    public async Task An_item_in_another_period_is_not_reachable_from_this_one()
    {
        var other = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(other);
        var theirs = AddLine(period: other);

        var result = await RemoveAsync(theirs.Id, periodId: _draft.Id);

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task Another_tenants_item_id_reads_as_not_found_and_survives()
    {
        var otherTenant = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var theirs = AddLine(tenantId: otherTenant);

        var result = await RemoveAsync(theirs.Id);

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Contains(theirs, _allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_period_reports_the_period_as_not_found()
    {
        var line = AddLine();

        var result = await RemoveAsync(line.Id, periodId: Guid.NewGuid());

        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_read_only_period_keeps_its_items(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);
        var line = AddLine(period: period);

        var result = await RemoveAsync(line.Id, periodId: period.Id);

        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_item_lookup()
    {
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed);
        _periods.Add(closed);

        var result = await RemoveAsync(Guid.NewGuid(), periodId: closed.Id);

        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
    }

    [Fact]
    public async Task An_open_period_allows_removal()
    {
        var open = TestBudgeting.PeriodIn(PeriodState.Open);
        _periods.Add(open);
        var line = AddLine(period: open);

        var result = await RemoveAsync(line.Id, periodId: open.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_item_whose_code_has_since_been_retired_can_still_be_removed()
    {
        var line = AddLine();
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await RemoveAsync(line.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }
}
