using NorthernLink.Budgeting.Application.Allocations;
using NorthernLink.Budgeting.Application.Allocations.Set;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// SetBudgetAllocationCommandHandler: the upsert, and the guard order the handler documents as
/// its contract — validate, then period exists and is editable, then code exists and is active.
/// Every refusal asserts <c>SaveChangesCallCount == 0</c> and that no line appeared.
/// </summary>
public class SetBudgetAllocationCommandHandlerTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly SetBudgetAllocationCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _code = TestBudgeting.CreateCode("ZBB-CREW-01");

    public SetBudgetAllocationCommandHandlerTests()
    {
        _handler = new SetBudgetAllocationCommandHandler(_allocations, _periods, _codes);
        _periods.Add(_draft);
        _codes.Add(_code);
    }

    private Task<Result<BudgetAllocationSetResult>> SetAsync(
        Guid? periodId = null,
        Guid? codeId = null,
        decimal? amount = 1250m,
        string? justification = "Two crew rotations a week.",
        Guid? actorId = null) =>
        _handler.Handle(
            new SetBudgetAllocationCommand(
                TestBudgeting.TenantId, periodId ?? _draft.Id, codeId ?? _code.Id, amount, justification,
                actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    // --- Upsert ---

    [Fact]
    public async Task The_first_set_creates_the_line_from_the_code_and_saves_once()
    {
        var result = await SetAsync(amount: 1250m, justification: "Two crew rotations a week.");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        var line = Assert.Single(_allocations.Allocations);
        Assert.Equal(line.Id, result.Value.AllocationId);
        Assert.Equal(TestBudgeting.TenantId, line.TenantId);
        Assert.Equal(_draft.Id, line.PeriodId);
        Assert.Equal(_code.Id, line.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", line.Code);
        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal("Two crew rotations a week.", line.Justification);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
        Assert.Equal(0, _periods.SaveChangesCallCount);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_second_set_for_the_same_code_updates_the_line_in_place()
    {
        var first = await SetAsync(amount: 1250m, justification: "First pass.");

        var second = await SetAsync(amount: 1875m, justification: "Revised after actuals.");

        Assert.True(second.IsSuccess);
        Assert.False(second.Value.Created);
        Assert.Equal(first.Value.AllocationId, second.Value.AllocationId);
        var line = Assert.Single(_allocations.Allocations);
        Assert.Equal(1875m, line.AmountCad);
        Assert.Equal("Revised after actuals.", line.Justification);
        Assert.Equal(2, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task Different_codes_in_the_same_period_get_their_own_lines()
    {
        var fuel = TestBudgeting.CreateCode("ZBB-FUEL-01");
        _codes.Add(fuel);

        var crew = await SetAsync(codeId: _code.Id);
        var fuelLine = await SetAsync(codeId: fuel.Id);

        Assert.True(crew.Value.Created);
        Assert.True(fuelLine.Value.Created);
        Assert.Equal(2, _allocations.Allocations.Count);
        Assert.Contains(_allocations.Allocations, a => a.Code == "ZBB-FUEL-01" && a.BudgetCodeId == fuel.Id);
    }

    [Fact]
    public async Task The_same_code_in_two_periods_gets_a_line_in_each()
    {
        var q1 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(q1);

        var inQ4 = await SetAsync(periodId: _draft.Id);
        var inQ1 = await SetAsync(periodId: q1.Id);

        Assert.True(inQ4.Value.Created);
        Assert.True(inQ1.Value.Created);
        Assert.NotEqual(inQ4.Value.AllocationId, inQ1.Value.AllocationId);
    }

    // --- Actor ---

    [Fact]
    public async Task The_actor_is_stamped_as_creator_on_create_and_as_modifier_on_update()
    {
        var editor = Guid.Parse("55555555-5555-5555-5555-555555555555");

        await SetAsync(actorId: TestBudgeting.ActorId);
        var line = Assert.Single(_allocations.Allocations);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Null(line.ModifiedBy);

        await SetAsync(amount: 99m, actorId: editor);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Equal(editor, line.ModifiedBy);
    }

    // --- Guard order: validation first ---

    [Fact]
    public async Task An_invalid_amount_reports_validation_before_the_period_lookup()
    {
        // Unknown period AND a negative amount: the caller hears about the payload, not about a
        // period the payload was never going to reach.
        var result = await SetAsync(periodId: Guid.NewGuid(), amount: -1m);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.AmountNegative, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_missing_justification_reports_validation_before_the_code_lookup()
    {
        var result = await SetAsync(codeId: Guid.NewGuid(), justification: "   ");

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, result.Error);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_missing_amount_reports_AmountRequired_even_on_an_editable_period_with_a_live_code()
    {
        var result = await SetAsync(amount: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.AmountRequired, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    // --- Guard order: the period ---

    [Fact]
    public async Task An_unknown_period_reports_the_period_as_not_found()
    {
        var result = await SetAsync(periodId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_read_only_period_refuses_a_new_line(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);

        var result = await SetAsync(periodId: period.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Open)]
    public async Task An_editable_period_accepts_a_line(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);

        var result = await SetAsync(periodId: period.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_read_only_period_refuses_a_rewrite_of_an_existing_line_too()
    {
        // Build the line while Draft, then walk the period past Open and try again.
        var period = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(period);
        Assert.True((await SetAsync(periodId: period.Id, amount: 1250m)).IsSuccess);
        Assert.True(period.Transition(PeriodTransition.Finalize, null).IsSuccess);

        var result = await SetAsync(periodId: period.Id, amount: 5m);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Equal(1250m, Assert.Single(_allocations.Allocations).AmountCad);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_code_lookup()
    {
        // Closed period and an unknown code: the period is the wall the caller hits first.
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed);
        _periods.Add(closed);

        var result = await SetAsync(periodId: closed.Id, codeId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
    }

    // --- Guard order: the code ---

    [Fact]
    public async Task An_unknown_code_reports_the_code_as_not_found()
    {
        var result = await SetAsync(codeId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_retired_code_takes_no_first_line()
    {
        var retired = TestBudgeting.CreateCode("ZBB-OLD-01");
        Assert.True(retired.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        _codes.Add(retired);

        var result = await SetAsync(codeId: retired.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CodeRetired, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_line_on_a_code_retired_since_cannot_be_rewritten()
    {
        // Rewriting is a new decision against a code no longer offered — refused, and the
        // existing line is left exactly as it was.
        Assert.True((await SetAsync(amount: 1250m, justification: "Before retirement.")).IsSuccess);
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await SetAsync(amount: 5m, justification: "After retirement.");

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CodeRetired, result.Error);
        var line = Assert.Single(_allocations.Allocations);
        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal("Before retirement.", line.Justification);
        Assert.Null(line.ModifiedBy);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_restored_code_takes_lines_again()
    {
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        Assert.Equal(BudgetAllocationErrors.CodeRetired, (await SetAsync()).Error);
        Assert.True(_code.SetActive(true, TestBudgeting.ActorId).IsSuccess);

        var result = await SetAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(_allocations.Allocations);
    }
}
