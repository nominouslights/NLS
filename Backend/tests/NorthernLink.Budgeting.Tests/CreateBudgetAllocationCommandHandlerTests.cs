using NorthernLink.Budgeting.Application.Allocations.Create;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// CreateBudgetAllocationCommandHandler: many items per code, and the guard order the handler
/// documents as its contract — code named, validate, then period exists and is editable, then
/// code exists and is active. Every refusal asserts <c>SaveChangesCallCount == 0</c> and that no
/// item appeared.
/// </summary>
public class CreateBudgetAllocationCommandHandlerTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly CreateBudgetAllocationCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _code = TestBudgeting.CreateCode("ZBB-CREW-01");

    public CreateBudgetAllocationCommandHandlerTests()
    {
        _handler = new CreateBudgetAllocationCommandHandler(_allocations, _periods, _codes);
        _periods.Add(_draft);
        _codes.Add(_code);
    }

    private Task<Result<Guid>> CreateAsync(
        Guid? periodId = null,
        Guid? codeId = null,
        BudgetItemDetails? details = null,
        Guid? actorId = null,
        bool omitCode = false) =>
        _handler.Handle(
            new CreateBudgetAllocationCommand(
                TestBudgeting.TenantId,
                periodId ?? _draft.Id,
                omitCode ? null : codeId ?? _code.Id,
                details ?? TestBudgeting.Item(),
                actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    // --- Creating ---

    [Fact]
    public async Task Create_adds_the_item_from_the_code_and_saves_once()
    {
        var result = await CreateAsync(details: TestBudgeting.Item(title: "Crew rotations", amount: 1250m));

        Assert.True(result.IsSuccess);
        var line = Assert.Single(_allocations.Allocations);
        Assert.Equal(line.Id, result.Value);
        Assert.Equal(TestBudgeting.TenantId, line.TenantId);
        Assert.Equal(_draft.Id, line.PeriodId);
        Assert.Equal(_code.Id, line.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", line.Code);
        Assert.Equal("Crew rotations", line.Title);
        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
        Assert.Equal(0, _periods.SaveChangesCallCount);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_code_can_hold_many_items_in_one_period()
    {
        // The whole change: a code's budget is the sum of its items, not one overwritten number.
        var tires = await CreateAsync(details: TestBudgeting.Item(title: "Winter tires", amount: 1800m));
        var brakes = await CreateAsync(details: TestBudgeting.Item(title: "Brake pads", amount: 450m));
        var same = await CreateAsync(details: TestBudgeting.Item(title: "Winter tires", amount: 1800m));

        Assert.True(tires.IsSuccess && brakes.IsSuccess && same.IsSuccess);
        Assert.Equal(3, _allocations.Allocations.Count);
        Assert.All(_allocations.Allocations, a => Assert.Equal(_code.Id, a.BudgetCodeId));
        Assert.Equal(3, _allocations.Allocations.Select(a => a.Id).Distinct().Count());
        Assert.Equal(4050m, _allocations.Allocations.Sum(a => a.AmountCad));
        Assert.Equal(3, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_built_up_item_is_stored_with_the_server_computed_amount()
    {
        var result = await CreateAsync(details: TestBudgeting.Item(
            amount: 1m, quantity: 12m, unitCost: 350.125m, unit: "month"));

        Assert.True(result.IsSuccess);
        var line = Assert.Single(_allocations.Allocations);
        // 350.125 → 350.13 (half away from zero), × 12 = 4201.56. The sent amount of 1 is ignored.
        Assert.Equal(350.13m, line.UnitCostCad);
        Assert.Equal(4201.56m, line.AmountCad);
    }

    [Fact]
    public async Task The_same_code_in_two_periods_gets_an_item_in_each()
    {
        var q1 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(q1);

        var inQ4 = await CreateAsync(periodId: _draft.Id);
        var inQ1 = await CreateAsync(periodId: q1.Id);

        Assert.NotEqual(inQ4.Value, inQ1.Value);
        Assert.Single(_allocations.Allocations, a => a.PeriodId == q1.Id);
    }

    // --- Guard order: input first ---

    [Fact]
    public async Task A_missing_code_reports_CodeRequired_before_anything_else()
    {
        var result = await CreateAsync(periodId: Guid.NewGuid(), details: TestBudgeting.Item(title: null), omitCode: true);

        Assert.Equal(BudgetAllocationErrors.CodeRequired, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_invalid_item_reports_validation_before_the_period_lookup()
    {
        // Unknown period AND a negative amount: the caller hears about the payload.
        var result = await CreateAsync(periodId: Guid.NewGuid(), details: TestBudgeting.Item(amount: -1m));

        Assert.Equal(BudgetAllocationErrors.AmountNegative, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_missing_justification_reports_validation_before_the_code_lookup()
    {
        var result = await CreateAsync(codeId: Guid.NewGuid(), details: TestBudgeting.Item(justification: "   "));

        Assert.Equal(BudgetAllocationErrors.JustificationRequired, result.Error);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task Half_a_build_up_is_refused_on_an_editable_period_with_a_live_code()
    {
        var result = await CreateAsync(details: TestBudgeting.Item(amount: 100m, quantity: 4m));

        Assert.Equal(BudgetAllocationErrors.QuantityWithoutUnitCost, result.Error);
        Assert.Empty(_allocations.Allocations);
    }

    // --- Guard order: the period ---

    [Fact]
    public async Task An_unknown_period_reports_the_period_as_not_found()
    {
        var result = await CreateAsync(periodId: Guid.NewGuid());

        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_read_only_period_refuses_a_new_item(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);

        var result = await CreateAsync(periodId: period.Id);

        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Open)]
    public async Task An_editable_period_accepts_an_item(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);

        var result = await CreateAsync(periodId: period.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_code_lookup()
    {
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed);
        _periods.Add(closed);

        var result = await CreateAsync(periodId: closed.Id, codeId: Guid.NewGuid());

        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
    }

    // --- Guard order: the code ---

    [Fact]
    public async Task An_unknown_code_reports_the_code_as_not_found()
    {
        var result = await CreateAsync(codeId: Guid.NewGuid());

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_retired_code_takes_no_item()
    {
        var retired = TestBudgeting.CreateCode("ZBB-OLD-01");
        Assert.True(retired.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        _codes.Add(retired);

        var result = await CreateAsync(codeId: retired.Id);

        Assert.Equal(BudgetAllocationErrors.CodeRetired, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_restored_code_takes_items_again()
    {
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        Assert.Equal(BudgetAllocationErrors.CodeRetired, (await CreateAsync()).Error);
        Assert.True(_code.SetActive(true, TestBudgeting.ActorId).IsSuccess);

        Assert.True((await CreateAsync()).IsSuccess);
        Assert.Single(_allocations.Allocations);
    }
}
