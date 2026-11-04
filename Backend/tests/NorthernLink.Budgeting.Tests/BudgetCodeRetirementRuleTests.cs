using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// A budget code cannot be retired while its own period uses it — its items, or the transactions
/// dated inside it — and the refusal names which: items can be removed first, spend cannot be got
/// round at all. Mostly driven through <see cref="SetBudgetCodeActiveCommandHandler"/>
/// rather than the rule class directly, because the rule only matters as the answer that endpoint
/// gives — including the parts the handler owns: the Draft/Open gate, the retire direction only,
/// and only on a real transition.
/// <para>
/// The arm most easily missed is a code referenced by a transaction and no item at all; it has
/// its own section below. The section on frozen periods pins that the Draft/Open chart gate answers
/// first, which awaits an owner decision.
/// </para>
/// </summary>
public class BudgetCodeRetirementRuleTests
{
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryActualTransactionRepository _actuals = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly SetBudgetCodeActiveCommandHandler _handler;

    public BudgetCodeRetirementRuleTests()
    {
        _handler = new SetBudgetCodeActiveCommandHandler(_codes, _allocations, _actuals, _periods);
    }

    /// <summary>A registered quarter of 2026 in the given state, with a FUEL code of its own.</summary>
    private (BudgetPeriod Period, BudgetCode Code) Chart(PeriodState state, int ordinal = 4, string code = "ZBB-FUEL-01")
    {
        var period = TestBudgeting.PeriodIn(state, PeriodGranularity.Quarter, 2026, ordinal);
        _periods.Add(period);
        var budgetCode = TestBudgeting.CreateCode(code, periodId: period.Id);
        _codes.Add(budgetCode);
        return (period, budgetCode);
    }

    private void Plan(BudgetPeriod period, BudgetCode code) =>
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, code.Id, code.Code));

    private void Spend(BudgetCode code, DateOnly occurredOn) =>
        _actuals.Add(TestBudgeting.CreateActual(code.Id, code.Code, occurredOn: occurredOn));

    private Task<Result> RetireAsync(BudgetPeriod period, Guid codeId) =>
        _handler.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, period.Id, codeId, false, TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> RestoreAsync(BudgetPeriod period, Guid codeId) =>
        _handler.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, period.Id, codeId, true, TestBudgeting.ActorId),
            CancellationToken.None);

    // --- Nothing in the code's period references it ---

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Open)]
    public async Task A_code_nothing_references_is_freely_retirable(PeriodState state)
    {
        var (period, code) = Chart(state);

        var result = await RetireAsync(period, code.Id);

        Assert.True(result.IsSuccess);
        Assert.False(code.IsActive);
        Assert.Equal(1, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Another_periods_items_on_the_same_string_do_not_block()
    {
        // Q3 plans against ITS FUEL and is still live. Q4's FUEL is another row nothing in Q4
        // uses — retiring it must not be refused because of Q3.
        var (q3, q3Fuel) = Chart(PeriodState.Open, ordinal: 3);
        Plan(q3, q3Fuel);
        var (q4, q4Fuel) = Chart(PeriodState.Draft, ordinal: 4);

        var result = await RetireAsync(q4, q4Fuel.Id);

        Assert.True(result.IsSuccess);
        Assert.False(q4Fuel.IsActive);
        Assert.True(q3Fuel.IsActive);
    }

    [Fact]
    public async Task Another_periods_spend_on_the_same_string_does_not_block()
    {
        var (_, q3Fuel) = Chart(PeriodState.Open, ordinal: 3);
        Spend(q3Fuel, new DateOnly(2026, 8, 14));
        var (q4, q4Fuel) = Chart(PeriodState.Draft, ordinal: 4);

        Assert.True((await RetireAsync(q4, q4Fuel.Id)).IsSuccess);
    }

    [Fact]
    public async Task An_item_on_a_different_code_of_the_same_period_does_not_block()
    {
        var (period, code) = Chart(PeriodState.Open);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, Guid.NewGuid(), "ZBB-CREW-01"));

        Assert.True((await RetireAsync(period, code.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_transaction_dated_outside_the_codes_period_does_not_block()
    {
        // Unreachable from this period's view, and the record handler would refuse to file it
        // against this period's code — so it must not freeze the code.
        var (period, code) = Chart(PeriodState.Open);
        Spend(code, new DateOnly(2030, 6, 1));

        Assert.True((await RetireAsync(period, code.Id)).IsSuccess);
    }

    // --- Blocked by the code's own live period, through an item ---

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Open)]
    public async Task An_item_in_the_codes_own_period_blocks_retirement(PeriodState state)
    {
        var (period, code) = Chart(state);
        Plan(period, code);

        var result = await RetireAsync(period, code.Id);

        Assert.Equal(BudgetCodeErrors.RetirementBlockedByItems(period.Label), result.Error);
        Assert.Equal("Budgeting.Code.PeriodNotClosed", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.True(code.IsActive);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_item_carrying_only_the_string_blocks_too()
    {
        // A recreated code: the old item pins the string, exactly as it does for the delete probe.
        var (period, code) = Chart(PeriodState.Open);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, Guid.NewGuid(), code.Code));

        Assert.True((await RetireAsync(period, code.Id)).IsFailure);
    }

    [Fact]
    public async Task Removing_the_item_is_the_way_out_for_a_planned_code()
    {
        // The plan is still editable in Draft and Open, so the planner can clear the item and
        // then retire the code.
        var (period, code) = Chart(PeriodState.Draft);
        Plan(period, code);
        Assert.True((await RetireAsync(period, code.Id)).IsFailure);

        _allocations.Allocations.RemoveAll(a => a.PeriodId == period.Id);

        Assert.True((await RetireAsync(period, code.Id)).IsSuccess);
    }

    // --- Blocked through a transaction and no item at all ---

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Open)]
    public async Task A_transaction_alone_blocks_retirement_with_no_item_anywhere(PeriodState state)
    {
        // The arm most likely to be missed. Skipping it would leave a live period's actuals
        // pointing at a code the ledger screen renders as retired.
        var (period, code) = Chart(state);
        Spend(code, period.StartsOn);

        var result = await RetireAsync(period, code.Id);

        Assert.Equal(BudgetCodeErrors.RetirementBlockedBySpend(period.Label), result.Error);
        Assert.Equal("Budgeting.Code.PeriodNotClosed", result.Error.Code);
        Assert.Empty(_allocations.Allocations);
        Assert.True(code.IsActive);
    }

    [Fact]
    public async Task A_transaction_on_the_periods_last_day_still_counts_as_inside_it()
    {
        // Inclusive at both ends, like BudgetPeriod.Covers — the mapping the record handler uses.
        var (period, code) = Chart(PeriodState.Open);
        Spend(code, period.EndsOn);

        Assert.True((await RetireAsync(period, code.Id)).IsFailure);
    }

    // --- Frozen periods: the chart gate answers first ---

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task Outside_Draft_and_Open_the_chart_gate_refuses_before_the_rule_runs(PeriodState state)
    {
        // Pinned so that relaxing the chart gate for retirement is a visible decision (an open
        // owner question): today a frozen period's codes cannot be retired at all, used or not.
        // See BudgetCodeRetirementRule.
        var (period, code) = Chart(state);
        Plan(period, code);

        var result = await RetireAsync(period, code.Id);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
        Assert.True(code.IsActive);
    }

    [Fact]
    public async Task The_rule_reports_an_unknown_period_as_not_found()
    {
        var result = await BudgetCodeRetirementRule.ValidateAsync(
            _allocations, _actuals, _periods, Guid.NewGuid(), Guid.NewGuid(), "ZBB-FUEL-01", CancellationToken.None);

        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
    }

    // --- The message is the user-facing copy, and names the remedy that actually works ---

    [Fact]
    public async Task Items_alone_tell_the_user_to_remove_them_first()
    {
        var (period, code) = Chart(PeriodState.Open);
        Plan(period, code);

        var message = (await RetireAsync(period, code.Id)).Error.Message;

        Assert.Equal(
            "FY2026 Q4 has budget items on this code. Remove its items first, then retire the code — a planned line has to keep resolving to a live code.",
            message);
        Assert.DoesNotContain("close", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Spend_says_the_code_cannot_be_retired()
    {
        var (period, code) = Chart(PeriodState.Open);
        Spend(code, period.StartsOn);

        var message = (await RetireAsync(period, code.Id)).Error.Message;

        Assert.Equal(
            "This budget code has spend recorded in FY2026 Q4. A code with spend recorded in its period cannot be retired — its transactions have to keep resolving to a live code.",
            message);
        Assert.DoesNotContain("close", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Spend_wins_over_items_when_both_apply()
    {
        // Removing the items would not free the code, so naming them would send the user down a
        // path that ends in the spend refusal anyway.
        var (period, code) = Chart(PeriodState.Draft);
        Plan(period, code);
        Spend(code, period.EndsOn);

        var result = await RetireAsync(period, code.Id);

        Assert.Equal(BudgetCodeErrors.RetirementBlockedBySpend(period.Label), result.Error);
    }

    // --- Direction and idempotence: the handler's half of the rule ---

    [Fact]
    public async Task Restoring_a_code_is_never_blocked_by_its_live_period()
    {
        // activate takes nothing away from anyone, so it carries no check at all.
        var (period, code) = Chart(PeriodState.Open);
        Plan(period, code);
        Assert.True(code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await RestoreAsync(period, code.Id);

        Assert.True(result.IsSuccess);
        Assert.True(code.IsActive);
    }

    [Fact]
    public async Task Re_retiring_an_already_retired_code_stays_the_idempotent_no_op()
    {
        // The guard runs on a transition, not on every request — otherwise a repeated click, or a
        // code retired before this rule existed, would start failing on a request that changes
        // nothing.
        var (period, code) = Chart(PeriodState.Open);
        Assert.True(code.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        Plan(period, code);

        var result = await RetireAsync(period, code.Id);

        Assert.True(result.IsSuccess);
        Assert.False(code.IsActive);
    }

    [Fact]
    public async Task An_unknown_code_is_still_reported_as_not_found_before_the_rule_runs()
    {
        var (period, code) = Chart(PeriodState.Open);
        Plan(period, code);

        var result = await RetireAsync(period, Guid.NewGuid());

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Retiring_a_parent_does_not_check_or_touch_its_children()
    {
        // Retirement does not cascade, so the children's items are none of this rule's business.
        var (period, parent) = Chart(PeriodState.Open);
        var child = TestBudgeting.CreateCode(
            "ZBB-FUEL-02", TestBudgeting.CodeDetails(parentCodeId: parent.Id), periodId: period.Id);
        _codes.Add(child);
        Plan(period, child);

        var result = await RetireAsync(period, parent.Id);

        Assert.True(result.IsSuccess);
        Assert.False(parent.IsActive);
        Assert.True(child.IsActive);
    }
}
