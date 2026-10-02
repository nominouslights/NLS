using NorthernLink.Budgeting.Application.Allocations.Update;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// UpdateBudgetAllocationCommandHandler: update by item id, moving an item between codes, and the
/// guard order — code named, validate, period exists and is editable, item exists in this period
/// (and this tenant), target code exists and is active. Every refusal asserts the item is
/// unchanged and nothing was saved.
/// </summary>
public class UpdateBudgetAllocationCommandHandlerTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly UpdateBudgetAllocationCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _code;
    private readonly BudgetAllocation _item;

    public UpdateBudgetAllocationCommandHandlerTests()
    {
        _handler = new UpdateBudgetAllocationCommandHandler(_allocations, _periods, _codes);
        _periods.Add(_draft);
        _code = AddCode(_draft, "ZBB-CREW-01");
        _item = TestBudgeting.CreateAllocation(
            _draft.Id, _code.Id, _code.Code, 1250m, "Original reasoning.", TestBudgeting.ActorId);
        _allocations.Add(_item);
    }

    /// <summary>Adds a code to one period's chart — codes belong to a period.</summary>
    private BudgetCode AddCode(BudgetPeriod period, string code)
    {
        var budgetCode = TestBudgeting.CreateCode(code, periodId: period.Id);
        _codes.Add(budgetCode);
        return budgetCode;
    }

    private Task<Result> UpdateAsync(
        Guid? periodId = null,
        Guid? allocationId = null,
        Guid? codeId = null,
        BudgetItemDetails? details = null,
        Guid? actorId = null,
        bool omitCode = false) =>
        _handler.Handle(
            new UpdateBudgetAllocationCommand(
                TestBudgeting.TenantId,
                periodId ?? _draft.Id,
                allocationId ?? _item.Id,
                omitCode ? null : codeId ?? _code.Id,
                details ?? TestBudgeting.Item(amount: 1875m, justification: "Revised after actuals."),
                actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    private void AssertUntouched()
    {
        Assert.Equal(1250m, _item.AmountCad);
        Assert.Equal("Original reasoning.", _item.Justification);
        Assert.Equal(_code.Id, _item.BudgetCodeId);
        Assert.Null(_item.ModifiedBy);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    // --- Updating ---

    [Fact]
    public async Task The_item_is_rewritten_in_place_and_saved_once()
    {
        var editor = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var result = await UpdateAsync(
            details: TestBudgeting.Item(title: "Crew, revised", amount: 1875m, justification: "Revised after actuals."),
            actorId: editor);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(_allocations.Allocations);
        Assert.Same(_item, line);
        Assert.Equal("Crew, revised", line.Title);
        Assert.Equal(1875m, line.AmountCad);
        Assert.Equal("Revised after actuals.", line.Justification);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Equal(editor, line.ModifiedBy);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task Only_the_addressed_item_changes_when_a_code_has_several()
    {
        var sibling = TestBudgeting.CreateAllocation(_draft.Id, _code.Id, _code.Code, 300m, "Sibling.");
        _allocations.Add(sibling);

        Assert.True((await UpdateAsync(allocationId: _item.Id)).IsSuccess);

        Assert.Equal(1875m, _item.AmountCad);
        Assert.Equal(300m, sibling.AmountCad);
        Assert.Equal("Sibling.", sibling.Justification);
    }

    [Fact]
    public async Task An_item_can_move_to_another_active_code()
    {
        var fuel = AddCode(_draft, "ZBB-FUEL-01");

        var result = await UpdateAsync(codeId: fuel.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(fuel.Id, _item.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", _item.Code);
        Assert.Equal(_draft.Id, _item.PeriodId);
    }

    [Fact]
    public async Task Moving_an_item_to_a_retired_code_is_refused_as_CodeRetired()
    {
        var retired = AddCode(_draft, "ZBB-OLD-01");
        Assert.True(retired.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await UpdateAsync(codeId: retired.Id);

        Assert.Equal(BudgetAllocationErrors.CodeRetired, result.Error);
        AssertUntouched();
    }

    [Fact]
    public async Task Moving_an_item_to_a_code_of_another_period_reports_the_code_as_not_found()
    {
        // Q1's ZBB-FUEL-01 is a real, active code — of another period's chart. An item's code
        // must belong to the item's period.
        var q1 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(q1);
        var fuelInQ1 = AddCode(q1, "ZBB-FUEL-01");

        var result = await UpdateAsync(codeId: fuelInQ1.Id);

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        AssertUntouched();
    }

    [Fact]
    public async Task Moving_an_item_to_an_unknown_code_reports_the_code_as_not_found()
    {
        var result = await UpdateAsync(codeId: Guid.NewGuid());

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        AssertUntouched();
    }

    [Fact]
    public async Task An_item_on_a_code_retired_since_cannot_be_rewritten_in_place_but_can_be_moved_off_it()
    {
        // Rewriting is a new decision against a code no longer offered — refused. Moving the
        // item to a live code in the same update is the way out CodeRetired names.
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        Assert.Equal(BudgetAllocationErrors.CodeRetired, (await UpdateAsync()).Error);
        AssertUntouched();

        var fuel = AddCode(_draft, "ZBB-FUEL-01");
        Assert.True((await UpdateAsync(codeId: fuel.Id)).IsSuccess);
        Assert.Equal(fuel.Id, _item.BudgetCodeId);
    }

    // --- Guard order: input first ---

    [Fact]
    public async Task A_missing_code_reports_CodeRequired()
    {
        var result = await UpdateAsync(omitCode: true);

        Assert.Equal(BudgetAllocationErrors.CodeRequired, result.Error);
        AssertUntouched();
    }

    [Fact]
    public async Task An_invalid_item_reports_validation_before_any_lookup()
    {
        // Unknown period, unknown item, AND an over-long title: the payload is the answer.
        var result = await UpdateAsync(
            periodId: Guid.NewGuid(), allocationId: Guid.NewGuid(),
            details: TestBudgeting.Item(title: new string('t', 121)));

        Assert.Equal(BudgetAllocationErrors.TitleTooLong, result.Error);
        AssertUntouched();
    }

    // --- Guard order: the period ---

    [Fact]
    public async Task An_unknown_period_reports_the_period_as_not_found()
    {
        var result = await UpdateAsync(periodId: Guid.NewGuid());

        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        AssertUntouched();
    }

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_read_only_period_refuses_the_rewrite(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state);
        _periods.Add(period);
        var line = TestBudgeting.CreateAllocation(period.Id, _code.Id, _code.Code, 1250m);
        _allocations.Add(line);

        var result = await UpdateAsync(periodId: period.Id, allocationId: line.Id);

        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_item_lookup()
    {
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed);
        _periods.Add(closed);

        var result = await UpdateAsync(periodId: closed.Id, allocationId: Guid.NewGuid());

        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
    }

    // --- Guard order: the item ---

    [Fact]
    public async Task An_unknown_item_id_reports_the_item_as_not_found()
    {
        var result = await UpdateAsync(allocationId: Guid.NewGuid());

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        AssertUntouched();
    }

    [Fact]
    public async Task An_item_of_another_period_is_not_reachable_from_this_one()
    {
        var q1 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(q1);

        // _item lives in _draft; addressing it through Q1's route must not find it.
        var result = await UpdateAsync(periodId: q1.Id, allocationId: _item.Id);

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        AssertUntouched();
    }

    [Fact]
    public async Task Another_tenants_item_id_reads_as_not_found()
    {
        // Same period id, real item id — but another tenant's row. The tenant query filter (and
        // RLS beneath it) hides it, so the caller learns nothing about its existence.
        var otherTenant = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var theirs = TestBudgeting.CreateAllocation(
            _draft.Id, _code.Id, _code.Code, 5000m, "Their reasoning.", tenantId: otherTenant);
        _allocations.Add(theirs);

        var result = await UpdateAsync(allocationId: theirs.Id);

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
        Assert.Equal(5000m, theirs.AmountCad);
        Assert.Equal("Their reasoning.", theirs.Justification);
        Assert.Null(theirs.ModifiedBy);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_item_lookup_runs_before_the_code_lookup()
    {
        // Unknown item AND unknown code: the item the route addresses is the answer.
        var result = await UpdateAsync(allocationId: Guid.NewGuid(), codeId: Guid.NewGuid());

        Assert.Equal(BudgetAllocationErrors.NotFound, result.Error);
    }

    // --- Copied items ---

    [Fact]
    public async Task A_copied_item_saves_only_once_it_is_argued()
    {
        var copy = _item.CopyInto(_draft.Id, _code.Id, null);
        _allocations.Add(copy);

        var unargued = await UpdateAsync(allocationId: copy.Id, details: TestBudgeting.Item(justification: "  "));
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, unargued.Error);
        Assert.True(copy.NeedsJustification);

        var argued = await UpdateAsync(allocationId: copy.Id, details: TestBudgeting.Item(justification: "Argued fresh."));
        Assert.True(argued.IsSuccess);
        Assert.False(copy.NeedsJustification);
    }
}
