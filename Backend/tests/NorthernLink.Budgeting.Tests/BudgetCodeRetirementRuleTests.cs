using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// A budget code can only be retired once every period that uses it is Closed. Driven through
/// <see cref="SetBudgetCodeActiveCommandHandler"/> rather than the rule class directly, because
/// the rule only matters as the answer that endpoint gives — including the parts the handler
/// owns: that it runs on the retire direction only, and only on a real transition.
/// <para>
/// The arm most easily missed is a code referenced by a transaction and no allocation line at
/// all; it has its own section below.
/// </para>
/// </summary>
public class BudgetCodeRetirementRuleTests
{
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryActualTransactionRepository _actuals = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly SetBudgetCodeActiveCommandHandler _handler;

    private readonly BudgetCode _code = TestBudgeting.CreateCode("ZBB-FUEL-01");

    public BudgetCodeRetirementRuleTests()
    {
        _handler = new SetBudgetCodeActiveCommandHandler(_codes, _allocations, _actuals, _periods);
        _codes.Add(_code);
    }

    private Task<Result> RetireAsync(Guid? codeId = null) =>
        _handler.Handle(
            new SetBudgetCodeActiveCommand(
                TestBudgeting.TenantId, codeId ?? _code.Id, IsActive: false, TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> RestoreAsync() =>
        _handler.Handle(
            new SetBudgetCodeActiveCommand(
                TestBudgeting.TenantId, _code.Id, IsActive: true, TestBudgeting.ActorId),
            CancellationToken.None);

    /// <summary>A period in the given state, registered, with an allocation line on the code.</summary>
    private BudgetPeriod PlannedIn(PeriodState state, int year = 2026, int ordinal = 4)
    {
        var period = TestBudgeting.PeriodIn(state, PeriodGranularity.Quarter, year, ordinal);
        _periods.Add(period);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, _code.Id, _code.Code));
        return period;
    }

    /// <summary>A period in the given state, registered, with a transaction dated inside it.</summary>
    private BudgetPeriod SpentIn(PeriodState state, int year = 2026, int ordinal = 4)
    {
        var period = TestBudgeting.PeriodIn(state, PeriodGranularity.Quarter, year, ordinal);
        _periods.Add(period);
        _actuals.Add(TestBudgeting.CreateActual(_code.Id, _code.Code, occurredOn: period.StartsOn));
        return period;
    }

    // --- Nothing references the code ---

    [Fact]
    public async Task A_code_nothing_references_is_freely_retirable()
    {
        _periods.Add(TestBudgeting.PeriodIn(PeriodState.Draft));

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
        Assert.False(_code.IsActive);
        Assert.Equal(1, _codes.SaveChangesCallCount);
    }

    // --- Blocked by a live period, through an allocation line ---

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.Open)]
    [InlineData(PeriodState.InReview)]
    public async Task A_period_that_plans_against_the_code_and_is_not_closed_blocks_retirement(PeriodState state)
    {
        PlannedIn(state);

        var result = await RetireAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Code.PeriodNotClosed", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.True(_code.IsActive);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_closed_period_that_plans_against_the_code_does_not_block_retirement()
    {
        PlannedIn(PeriodState.Closed);

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
        Assert.False(_code.IsActive);
    }

    [Fact]
    public async Task Every_referencing_period_has_to_be_closed_not_just_one_of_them()
    {
        PlannedIn(PeriodState.Closed, 2026, 3);
        PlannedIn(PeriodState.Open, 2026, 4);

        var result = await RetireAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Code.PeriodNotClosed", result.Error.Code);
    }

    [Fact]
    public async Task Once_the_last_live_period_closes_the_code_retires()
    {
        PlannedIn(PeriodState.Closed, 2026, 3);
        var open = PlannedIn(PeriodState.Open, 2026, 4);
        Assert.True((await RetireAsync()).IsFailure);

        Assert.True(open.Transition(PeriodTransition.BeginReview, null).IsSuccess);
        Assert.True(open.Transition(PeriodTransition.Close, null).IsSuccess);

        Assert.True((await RetireAsync()).IsSuccess);
        Assert.False(_code.IsActive);
    }

    [Fact]
    public async Task Removing_the_line_from_a_draft_period_is_the_documented_way_out()
    {
        // The consequence spelled out on the rule: a Draft or Open period's plan is still
        // editable, so the planner can clear the line and then retire the code.
        var draft = PlannedIn(PeriodState.Draft);
        Assert.True((await RetireAsync()).IsFailure);

        _allocations.Allocations.RemoveAll(a => a.PeriodId == draft.Id);

        Assert.True((await RetireAsync()).IsSuccess);
    }

    [Fact]
    public async Task A_period_that_plans_against_a_different_code_does_not_block_anything()
    {
        var period = TestBudgeting.PeriodIn(PeriodState.Open);
        _periods.Add(period);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, Guid.NewGuid(), "ZBB-CREW-01"));

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
    }

    // --- Blocked by a live period, through a transaction and no allocation line at all ---

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.Open)]
    [InlineData(PeriodState.InReview)]
    public async Task A_transaction_alone_blocks_retirement_with_no_allocation_line_anywhere(PeriodState state)
    {
        // The arm most likely to be missed. Skipping it would leave a live period's actuals
        // pointing at a code the ledger screen renders as retired.
        SpentIn(state);

        var result = await RetireAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Code.PeriodNotClosed", result.Error.Code);
        Assert.Empty(_allocations.Allocations);
        Assert.True(_code.IsActive);
    }

    [Fact]
    public async Task A_transaction_in_a_closed_period_does_not_block_retirement()
    {
        SpentIn(PeriodState.Closed);

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
        Assert.False(_code.IsActive);
    }

    [Fact]
    public async Task A_transaction_on_the_periods_last_day_still_counts_as_inside_it()
    {
        // The date-to-period mapping is BudgetPeriod.Covers, inclusive at both ends — the same
        // one the record handler uses, so the two can never disagree about a boundary day.
        var period = TestBudgeting.PeriodIn(PeriodState.Open);
        _periods.Add(period);
        _actuals.Add(TestBudgeting.CreateActual(_code.Id, _code.Code, occurredOn: period.EndsOn));

        Assert.True((await RetireAsync()).IsFailure);
    }

    [Fact]
    public async Task A_transaction_dated_outside_every_period_cannot_freeze_a_code_forever()
    {
        // Unreachable from the period view by definition, and the record handler refuses to
        // create one — so it predates the rule or was written by hand. There is no period anyone
        // could close to release the code, so it must not block.
        _periods.Add(TestBudgeting.PeriodIn(PeriodState.Open));
        _actuals.Add(TestBudgeting.CreateActual(
            _code.Id, _code.Code, occurredOn: new DateOnly(2030, 6, 1)));

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_plan_and_a_spend_in_two_different_live_periods_both_have_to_clear()
    {
        PlannedIn(PeriodState.Open, 2026, 3);
        SpentIn(PeriodState.Draft, 2027, 1);

        var result = await RetireAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Code.PeriodNotClosed", result.Error.Code);
    }

    // --- The message is the user-facing copy ---

    [Fact]
    public async Task The_message_names_the_single_blocking_period_by_label()
    {
        // "a period is not closed" leaves the user hunting; "FY2026 Q4 is still open" does not.
        PlannedIn(PeriodState.Open, 2026, 4);

        var result = await RetireAsync();

        Assert.Contains("FY2026 Q4", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("every period that uses it is closed", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Several_blocking_periods_are_all_named_earliest_first()
    {
        PlannedIn(PeriodState.Open, 2026, 4);
        PlannedIn(PeriodState.Draft, 2026, 3);

        var result = await RetireAsync();

        var message = result.Error.Message;
        Assert.Contains("FY2026 Q3", message, StringComparison.Ordinal);
        Assert.Contains("FY2026 Q4", message, StringComparison.Ordinal);
        Assert.True(
            message.IndexOf("FY2026 Q3", StringComparison.Ordinal)
            < message.IndexOf("FY2026 Q4", StringComparison.Ordinal),
            $"Blocking periods should be named earliest first: {message}");
    }

    [Fact]
    public async Task A_long_list_of_blocking_periods_is_capped_and_summarized()
    {
        // A code a dozen periods use needs the rule and a couple of examples, not a paragraph of
        // period names.
        for (var ordinal = 1; ordinal <= 4; ordinal++)
        {
            PlannedIn(PeriodState.Open, 2026, ordinal);
        }

        PlannedIn(PeriodState.Open, 2027, 1);

        var result = await RetireAsync();

        var message = result.Error.Message;
        Assert.Contains("FY2026 Q1", message, StringComparison.Ordinal);
        Assert.Contains("2 more periods", message, StringComparison.Ordinal);
        Assert.DoesNotContain("FY2027 Q1", message, StringComparison.Ordinal);
    }

    // --- Direction and idempotence: the handler's half of the rule ---

    [Fact]
    public async Task Restoring_a_code_is_never_blocked_by_an_open_period()
    {
        // activate takes nothing away from anyone, so it carries no check at all.
        PlannedIn(PeriodState.Open);
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await RestoreAsync();

        Assert.True(result.IsSuccess);
        Assert.True(_code.IsActive);
    }

    [Fact]
    public async Task Re_retiring_an_already_retired_code_stays_the_idempotent_no_op()
    {
        // The guard runs on a transition, not on every request — otherwise a repeated click, or a
        // code retired before this rule existed, would start failing on a request that changes
        // nothing.
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        PlannedIn(PeriodState.Open);

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
        Assert.False(_code.IsActive);
    }

    [Fact]
    public async Task An_unknown_code_is_still_reported_as_not_found_before_the_rule_runs()
    {
        PlannedIn(PeriodState.Open);

        var result = await RetireAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Retiring_a_parent_does_not_check_or_touch_its_children()
    {
        // Retirement does not cascade, so the children's periods are none of this rule's
        // business — nothing happens to them.
        var child = TestBudgeting.CreateCode(
            "ZBB-FUEL-02", TestBudgeting.CodeDetails(parentCodeId: _code.Id));
        _codes.Add(child);
        var period = TestBudgeting.PeriodIn(PeriodState.Open);
        _periods.Add(period);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, child.Id, child.Code));

        var result = await RetireAsync();

        Assert.True(result.IsSuccess);
        Assert.False(_code.IsActive);
        Assert.True(child.IsActive);
    }
}
