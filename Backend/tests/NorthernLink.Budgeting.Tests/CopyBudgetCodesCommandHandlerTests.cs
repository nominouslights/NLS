using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Application.Codes.CopyFromPeriod;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Codes.Events;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// CopyBudgetCodesCommandHandler: the guard order (source named, source ≠ target, target exists
/// and is Draft/Open, source exists — in any state), the two skips (retired source code; string
/// already in the target, active or retired), the hierarchy remap by string, idempotency, and the
/// accounting invariant <c>copied + skippedExisting + skippedRetired == sourceCodeCount</c>.
/// </summary>
public class CopyBudgetCodesCommandHandlerTests
{
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly CopyBudgetCodesCommandHandler _handler;

    private readonly BudgetPeriod _source = TestBudgeting.PeriodIn(PeriodState.Closed, PeriodGranularity.Quarter, 2026, 3);
    private readonly BudgetPeriod _target = TestBudgeting.PeriodIn(PeriodState.Draft, PeriodGranularity.Quarter, 2026, 4);

    public CopyBudgetCodesCommandHandlerTests()
    {
        _handler = new CopyBudgetCodesCommandHandler(_codes, _periods);
        _periods.Add(_source);
        _periods.Add(_target);
    }

    private Task<Result<BudgetCodeCopyResult>> CopyAsync(
        Guid? periodId = null, Guid? sourcePeriodId = null, bool omitSource = false, Guid? actorId = null) =>
        _handler.Handle(
            new CopyBudgetCodesCommand(
                TestBudgeting.TenantId,
                periodId ?? _target.Id,
                omitSource ? null : sourcePeriodId ?? _source.Id,
                actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    private BudgetCode AddCode(BudgetPeriod period, string code, Guid? parentId = null, bool active = true, BudgetCodeDetails? details = null)
    {
        var budgetCode = TestBudgeting.CreateCode(
            code, details ?? TestBudgeting.CodeDetails(name: code + " name", parentCodeId: parentId), periodId: period.Id);
        if (!active)
        {
            Assert.True(budgetCode.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        }

        budgetCode.ClearDomainEvents();
        _codes.Add(budgetCode);
        return budgetCode;
    }

    private List<BudgetCode> TargetChart() => _codes.Codes.Where(c => c.PeriodId == _target.Id).ToList();

    private BudgetCode InTarget(string code) => Assert.Single(TargetChart(), c => c.Code == code);

    private static void AssertAddsUp(BudgetCodeCopyResult counts) =>
        Assert.Equal(counts.SourceCodeCount, counts.Copied + counts.SkippedExisting + counts.SkippedRetired);

    // --- Guards ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_missing_source_is_CopySourceRequired_before_any_lookup()
    {
        var result = await CopyAsync(periodId: Guid.NewGuid(), omitSource: true);

        Assert.Equal(BudgetCodeErrors.CopySourceRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Copying_a_period_onto_itself_is_refused()
    {
        AddCode(_target, "FUEL");

        var result = await CopyAsync(periodId: _target.Id, sourcePeriodId: _target.Id);

        Assert.Equal(BudgetCodeErrors.CopySourceIsTarget, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_target_is_period_not_found()
    {
        var result = await CopyAsync(periodId: Guid.NewGuid());

        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task An_unknown_source_is_CopySourceNotFound()
    {
        var result = await CopyAsync(sourcePeriodId: Guid.NewGuid());

        Assert.Equal(BudgetCodeErrors.CopySourceNotFound, result.Error);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Budgeting.Code.CopySourceNotFound", result.Error.Code);
    }

    [Theory]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_target_that_is_not_Draft_or_Open_is_refused_as_a_conflict(PeriodState state)
    {
        var target = TestBudgeting.PeriodIn(state, PeriodGranularity.Month, 2026, 11);
        _periods.Add(target);
        AddCode(_source, "FUEL");

        var result = await CopyAsync(periodId: target.Id);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.DoesNotContain(_codes.Codes, c => c.PeriodId == target.Id);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_target_guard_runs_before_the_source_lookup()
    {
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed, PeriodGranularity.Month, 2026, 12);
        _periods.Add(closed);

        var result = await CopyAsync(periodId: closed.Id, sourcePeriodId: Guid.NewGuid());

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
    }

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.Open)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_source_in_any_state_can_be_copied_from(PeriodState state)
    {
        // A Closed source is the common case — last quarter's chart into this quarter's Draft.
        var source = TestBudgeting.PeriodIn(state, PeriodGranularity.Month, 2026, 10);
        _periods.Add(source);
        AddCode(source, "FUEL");

        var result = await CopyAsync(sourcePeriodId: source.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Copied);
    }

    // --- Copying --------------------------------------------------------------------------------

    [Fact]
    public async Task Every_field_is_carried_across_as_an_active_code_with_a_new_id()
    {
        var owner = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var source = AddCode(_source, "ZBB-FUEL-01", details: TestBudgeting.CodeDetails(
            name: "Fuel",
            category: BudgetCodeCategory.Expense,
            reviewFrequency: BudgetReviewFrequency.Monthly,
            serviceLine: BudgetServiceLine.Fleet,
            costCentre: "CC-7",
            glAccountCode: "5100",
            taxTreatment: BudgetTaxTreatment.GstApplicable,
            budgetOwnerUserId: owner,
            description: "Diesel and gas."));
        var copier = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var result = await CopyAsync(actorId: copier);

        Assert.Equal(new BudgetCodeCopyResult(1, 0, 0, 1), result.Value);
        var copy = InTarget("ZBB-FUEL-01");
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(TestBudgeting.TenantId, copy.TenantId);
        Assert.Equal(_target.Id, copy.PeriodId);
        Assert.Equal("Fuel", copy.Name);
        Assert.Equal("Diesel and gas.", copy.Description);
        Assert.Equal(BudgetCodeCategory.Expense, copy.Category);
        Assert.Equal(BudgetServiceLine.Fleet, copy.ServiceLine);
        Assert.Equal("CC-7", copy.CostCentre);
        Assert.Equal("5100", copy.GlAccountCode);
        Assert.Equal(BudgetTaxTreatment.GstApplicable, copy.TaxTreatment);
        Assert.Equal(owner, copy.BudgetOwnerUserId);
        Assert.Equal(BudgetReviewFrequency.Monthly, copy.ReviewFrequency);
        Assert.True(copy.IsActive);
        Assert.Null(copy.ParentCodeId);
        Assert.Equal(copier, copy.CreatedBy);
        Assert.Null(copy.ModifiedBy);
        var created = Assert.Single(copy.DomainEvents.OfType<BudgetCodeCreatedDomainEvent>());
        Assert.Equal(_target.Id, created.PeriodId);
        Assert.Equal(1, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_source_chart_is_left_exactly_as_it_was()
    {
        var source = AddCode(_source, "FUEL");

        await CopyAsync();

        Assert.Equal(_source.Id, source.PeriodId);
        Assert.Single(_codes.Codes, c => c.PeriodId == _source.Id);
        Assert.Empty(source.DomainEvents);
    }

    // --- Hierarchy ------------------------------------------------------------------------------

    [Fact]
    public async Task Children_roll_up_into_the_targets_copy_of_their_parent()
    {
        // Added child-first on purpose: the handler must still copy the parent before the child.
        var fleet = TestBudgeting.CreateCode("FLEET", TestBudgeting.CodeDetails(name: "Fleet"), periodId: _source.Id);
        var fuel = AddCode(_source, "FUEL", parentId: fleet.Id);
        var tires = AddCode(_source, "TIRES", parentId: fleet.Id);
        _codes.Add(fleet);

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(3, 0, 0, 3), result.Value);
        var fleetCopy = InTarget("FLEET");
        Assert.NotEqual(fleet.Id, fleetCopy.Id);
        Assert.Null(fleetCopy.ParentCodeId);
        Assert.Equal(fleetCopy.Id, InTarget("FUEL").ParentCodeId);
        Assert.Equal(fleetCopy.Id, InTarget("TIRES").ParentCodeId);
        // Never the source parent's id — that names a code of another period.
        Assert.DoesNotContain(TargetChart(), c => c.ParentCodeId == fleet.Id);
        Assert.NotEqual(fuel.Id, InTarget("FUEL").Id);
        Assert.NotEqual(tires.Id, InTarget("TIRES").Id);
    }

    [Fact]
    public async Task A_child_rolls_up_into_a_parent_the_target_already_had()
    {
        var fleet = AddCode(_source, "FLEET");
        AddCode(_source, "FUEL", parentId: fleet.Id);
        var existingFleet = AddCode(_target, "FLEET");

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(1, 1, 0, 2), result.Value);
        Assert.Equal(existingFleet.Id, InTarget("FUEL").ParentCodeId);
    }

    [Fact]
    public async Task A_child_whose_parent_was_retired_and_skipped_is_copied_top_level()
    {
        var fleet = AddCode(_source, "FLEET", active: false);
        AddCode(_source, "FUEL", parentId: fleet.Id);

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(1, 0, 1, 2), result.Value);
        Assert.Null(InTarget("FUEL").ParentCodeId);
        Assert.DoesNotContain(TargetChart(), c => c.Code == "FLEET");
    }

    [Fact]
    public async Task A_retired_source_parent_still_resolves_to_an_existing_target_code_of_that_string()
    {
        var fleet = AddCode(_source, "FLEET", active: false);
        AddCode(_source, "FUEL", parentId: fleet.Id);
        var targetFleet = AddCode(_target, "FLEET");

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(1, 0, 1, 2), result.Value);
        Assert.Equal(targetFleet.Id, InTarget("FUEL").ParentCodeId);
    }

    [Fact]
    public async Task The_one_level_rule_is_never_broken_by_a_copy()
    {
        // In the target, FLEET already rolls up into OPS. A copied FUEL whose source parent is
        // FLEET cannot roll up into it (that would be two levels), so it is copied top-level.
        var fleet = AddCode(_source, "FLEET");
        AddCode(_source, "FUEL", parentId: fleet.Id);
        var ops = AddCode(_target, "OPS");
        AddCode(_target, "FLEET", parentId: ops.Id);

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(1, 1, 0, 2), result.Value);
        Assert.Null(InTarget("FUEL").ParentCodeId);
    }

    // --- Skips ----------------------------------------------------------------------------------

    [Fact]
    public async Task Retired_source_codes_are_not_copied()
    {
        AddCode(_source, "OLD", active: false);
        AddCode(_source, "FUEL");

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(1, 0, 1, 2), result.Value);
        Assert.DoesNotContain(TargetChart(), c => c.Code == "OLD");
    }

    [Fact]
    public async Task A_string_the_target_already_has_is_skipped_and_never_overwritten()
    {
        AddCode(_source, "FUEL", details: TestBudgeting.CodeDetails(name: "Source fuel"));
        var mine = AddCode(_target, "FUEL", details: TestBudgeting.CodeDetails(name: "My fuel"));

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(0, 1, 0, 1), result.Value);
        Assert.Same(mine, InTarget("FUEL"));
        Assert.Equal("My fuel", mine.Name);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_string_retired_in_the_target_is_skipped_too_not_resurrected()
    {
        // Retiring FUEL in this period was a decision; a copy must not undo it (or collide with it
        // on the unique index).
        AddCode(_source, "FUEL");
        var retiredHere = AddCode(_target, "FUEL", active: false);

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(0, 1, 0, 1), result.Value);
        Assert.False(retiredHere.IsActive);
        Assert.Single(TargetChart());
    }

    [Fact]
    public async Task A_code_retired_in_the_source_and_present_in_the_target_counts_as_retired()
    {
        // Precedence: the retired check runs first — "retired codes are never copied" is the
        // answer about the source code itself.
        AddCode(_source, "OLD", active: false);
        AddCode(_target, "OLD");

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(0, 0, 1, 1), result.Value);
    }

    [Fact]
    public async Task An_empty_source_chart_is_a_success_with_zeroes()
    {
        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetCodeCopyResult(0, 0, 0, 0), result.Value);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Running_the_copy_twice_copies_nothing_the_second_time()
    {
        var fleet = AddCode(_source, "FLEET");
        AddCode(_source, "FUEL", parentId: fleet.Id);
        AddCode(_source, "OLD", active: false);

        var first = await CopyAsync();
        var second = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(2, 0, 1, 3), first.Value);
        Assert.Equal(new BudgetCodeCopyResult(0, 2, 1, 3), second.Value);
        Assert.Equal(2, TargetChart().Count);
        Assert.Equal(1, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_counts_always_account_for_every_source_code()
    {
        var fleet = AddCode(_source, "FLEET");
        AddCode(_source, "FUEL", parentId: fleet.Id);   // copied under FLEET's copy
        AddCode(_source, "ADMIN");                      // already in the target
        AddCode(_source, "OLD", active: false);         // retired
        AddCode(_source, "WAGES");                      // copied
        AddCode(_target, "ADMIN");

        var result = await CopyAsync();

        var counts = result.Value;
        Assert.Equal(5, counts.SourceCodeCount);
        Assert.Equal(3, counts.Copied);
        Assert.Equal(1, counts.SkippedExisting);
        Assert.Equal(1, counts.SkippedRetired);
        AssertAddsUp(counts);
        Assert.Equal(4, TargetChart().Count);
    }

    [Fact]
    public async Task Codes_of_other_periods_are_neither_read_nor_touched()
    {
        var elsewhere = TestBudgeting.PeriodIn(PeriodState.Draft, PeriodGranularity.Month, 2026, 9);
        _periods.Add(elsewhere);
        var bystander = AddCode(elsewhere, "BYSTANDER");
        AddCode(_source, "FUEL");

        var result = await CopyAsync();

        Assert.Equal(new BudgetCodeCopyResult(1, 0, 0, 1), result.Value);
        Assert.DoesNotContain(TargetChart(), c => c.Code == "BYSTANDER");
        Assert.Equal(elsewhere.Id, bystander.PeriodId);
    }
}
