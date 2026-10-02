using NorthernLink.Budgeting.Application.Allocations;
using NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;
using NorthernLink.Budgeting.Application.Allocations.Update;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// CopyBudgetAllocationsCommandHandler: the guard order the handler documents as its contract
/// (source named, source ≠ target, then the <em>target</em> exists and is editable, then the
/// source exists — with no editability check on the source), the skip rules, and the accounting
/// invariant <c>copied + skippedAlreadyPlanned + skippedRetiredCode == sourceLineCount</c>. Every
/// refusal asserts <c>SaveChangesCallCount == 0</c> and that no line appeared in the target.
/// <para>
/// <b>Codes belong to a period</b>, so every code here exists twice — once in each period's chart,
/// with different ids and the same string — and a copied item must land on the <em>target's</em>
/// code. The mapping is by string; the "no active code with that string in this period" cases pin
/// what happens when the target's chart lacks or has retired the string.
/// </para>
/// <para>
/// The case that matters most is <see cref="Every_copied_line_arrives_with_no_justification_and_must_be_re_argued"/>:
/// it is what separates this from a plain duplicate.
/// </para>
/// </summary>
public class CopyBudgetAllocationsCommandHandlerTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly CopyBudgetAllocationsCommandHandler _handler;

    private readonly BudgetPeriod _target = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4);
    private readonly BudgetPeriod _source = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 3);

    /// <summary>ZBB-CREW-01 in the source period's chart.</summary>
    private readonly BudgetCode _crewSource;

    /// <summary>ZBB-CREW-01 in the target period's chart — same string, different id.</summary>
    private readonly BudgetCode _crew;

    public CopyBudgetAllocationsCommandHandlerTests()
    {
        _handler = new CopyBudgetAllocationsCommandHandler(_allocations, _periods, _codes);
        _periods.Add(_target);
        _periods.Add(_source);
        _crewSource = AddCode(_source, "ZBB-CREW-01");
        _crew = AddCode(_target, "ZBB-CREW-01");
    }

    private Task<Result<BudgetAllocationCopyResult>> CopyAsync(
        Guid? periodId = null,
        Guid? sourcePeriodId = null,
        Guid? actorId = null,
        bool omitSource = false) =>
        _handler.Handle(
            new CopyBudgetAllocationsCommand(
                TestBudgeting.TenantId,
                periodId ?? _target.Id,
                omitSource ? null : sourcePeriodId ?? _source.Id,
                actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    /// <summary>Puts an existing, fully argued line into a period — the state a copy reads from.</summary>
    private BudgetAllocation Plan(
        BudgetPeriod period,
        Guid budgetCodeId,
        string code = "ZBB-CREW-01",
        decimal amount = 1250m,
        string justification = "Two crew rotations a week.")
    {
        var line = TestBudgeting.CreateAllocation(
            period.Id, budgetCodeId, code, amount, justification, TestBudgeting.ActorId);
        _allocations.Add(line);
        return line;
    }

    /// <summary>Adds a code to one period's chart.</summary>
    private BudgetCode AddCode(BudgetPeriod period, string code, bool active = true)
    {
        var budgetCode = TestBudgeting.CreateCode(code, periodId: period.Id);
        if (!active)
        {
            Assert.True(budgetCode.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        }

        _codes.Add(budgetCode);
        return budgetCode;
    }

    /// <summary>Adds a code with that string to BOTH charts; returns (source copy, target copy).</summary>
    private (BudgetCode Source, BudgetCode Target) AddCodeToBoth(
        string code, bool activeInSource = true, bool activeInTarget = true) =>
        (AddCode(_source, code, activeInSource), AddCode(_target, code, activeInTarget));

    private List<BudgetAllocation> TargetLines() =>
        _allocations.Allocations.Where(a => a.PeriodId == _target.Id).ToList();

    // --- Guard order: validate the input, before any lookup ---

    [Fact]
    public async Task A_missing_source_reports_CopySourceRequired_before_the_period_lookup()
    {
        // Unknown target AND no source: the caller hears about the payload, not about a period
        // the payload was never going to reach.
        var result = await CopyAsync(periodId: Guid.NewGuid(), omitSource: true);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CopySourceRequired, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_missing_source_is_refused_even_on_a_perfectly_good_target()
    {
        var result = await CopyAsync(omitSource: true);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CopySourceRequired, result.Error);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_period_cannot_be_copied_onto_itself()
    {
        // Otherwise this is a baffling 200 that skips every line as "already planned".
        Plan(_target, _crew.Id);

        var result = await CopyAsync(periodId: _target.Id, sourcePeriodId: _target.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CopySourceIsTarget, result.Error);
        Assert.Single(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task Self_copy_is_refused_before_the_period_is_even_looked_up()
    {
        var unknown = Guid.NewGuid();

        var result = await CopyAsync(periodId: unknown, sourcePeriodId: unknown);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CopySourceIsTarget, result.Error);
    }

    // --- Guard order: the target period, before the source ---

    [Fact]
    public async Task An_unknown_target_period_reports_the_period_as_not_found()
    {
        var result = await CopyAsync(periodId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Draft, true)]
    [InlineData(PeriodState.Finalized, false)]
    [InlineData(PeriodState.Open, true)]
    [InlineData(PeriodState.InReview, false)]
    [InlineData(PeriodState.Closed, false)]
    public async Task Only_a_target_that_allows_plan_changes_accepts_a_copy(PeriodState state, bool editable)
    {
        // The same rule as the create handler's, over all five states: Draft and Open take lines,
        // the three read-only states refuse the whole request.
        var target = TestBudgeting.PeriodIn(state, PeriodGranularity.Month, 2026, 5);
        _periods.Add(target);
        AddCode(target, "ZBB-CREW-01");
        Plan(_source, _crewSource.Id);

        var result = await CopyAsync(periodId: target.Id);

        if (editable)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.Copied);
            Assert.Equal(1, _allocations.SaveChangesCallCount);
        }
        else
        {
            Assert.True(result.IsFailure);
            Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
            Assert.DoesNotContain(_allocations.Allocations, a => a.PeriodId == target.Id);
            Assert.Equal(0, _allocations.SaveChangesCallCount);
        }
    }

    [Fact]
    public async Task The_target_guard_runs_before_the_source_lookup()
    {
        // Closed target and an unknown source: the target is the wall the caller hits first,
        // because it is the resource the route addresses and where the write would land.
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed, PeriodGranularity.Month, 2026, 6);
        _periods.Add(closed);

        var result = await CopyAsync(periodId: closed.Id, sourcePeriodId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.PeriodNotEditable, result.Error);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    // --- Guard order: the source period (existence only — never editability) ---

    [Fact]
    public async Task An_unknown_source_period_reports_CopySourceNotFound()
    {
        // Its own error, not BudgetPeriodErrors.NotFound: two period ids are in play and the
        // console has to say which one was wrong.
        var result = await CopyAsync(sourcePeriodId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.CopySourceNotFound, result.Error);
        Assert.Equal("Budgeting.Allocation.CopySourceNotFound", result.Error.Code);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.Open)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_source_in_any_state_can_be_copied_from(PeriodState state)
    {
        // There is deliberately NO AllowsPlanChanges check on the source: copying last quarter's
        // Closed plan into a fresh Draft is the entire point of the feature. Nothing is written
        // to the source, so its state has no bearing.
        var source = TestBudgeting.PeriodIn(state, PeriodGranularity.Month, 2026, 7);
        _periods.Add(source);
        var crewThere = AddCode(source, "ZBB-CREW-01");
        Plan(source, crewThere.Id, amount: 4000m);

        var result = await CopyAsync(sourcePeriodId: source.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Copied);
        Assert.Equal(4000m, Assert.Single(TargetLines()).AmountCad);
    }

    // --- Copying: codes are matched by string ---

    [Fact]
    public async Task Every_source_line_lands_on_the_target_periods_code_with_the_same_string()
    {
        var fuel = AddCodeToBoth("ZBB-FUEL-01");
        Plan(_source, _crewSource.Id, "ZBB-CREW-01", 1250m);
        Plan(_source, fuel.Source.Id, "ZBB-FUEL-01", 480.50m);

        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetAllocationCopyResult(2, 0, 0, 2), result.Value);
        var lines = TargetLines();
        Assert.Equal(2, lines.Count);
        // The TARGET's code ids, never the source's: an item always points at a code of its own period.
        Assert.Contains(lines, a => a.Code == "ZBB-CREW-01" && a.BudgetCodeId == _crew.Id && a.AmountCad == 1250m);
        Assert.Contains(lines, a => a.Code == "ZBB-FUEL-01" && a.BudgetCodeId == fuel.Target.Id && a.AmountCad == 480.50m);
        Assert.DoesNotContain(lines, a => a.BudgetCodeId == _crewSource.Id || a.BudgetCodeId == fuel.Source.Id);
        Assert.All(lines, a => Assert.Equal(TestBudgeting.TenantId, a.TenantId));
        // One save for the whole copy, not one per line.
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_copier_is_stamped_as_the_creator_of_every_copied_line()
    {
        var copier = Guid.Parse("55555555-5555-5555-5555-555555555555");
        Plan(_source, _crewSource.Id);

        await CopyAsync(actorId: copier);

        var line = Assert.Single(TargetLines());
        Assert.Equal(copier, line.CreatedBy);
        Assert.Null(line.ModifiedBy);
    }

    [Fact]
    public async Task The_source_period_is_left_exactly_as_it_was()
    {
        Plan(_source, _crewSource.Id, amount: 1250m, justification: "The original argument.");

        await CopyAsync();

        var sourceLine = Assert.Single(_allocations.Allocations, a => a.PeriodId == _source.Id);
        Assert.Equal(1250m, sourceLine.AmountCad);
        Assert.Equal("The original argument.", sourceLine.Justification);
        Assert.Equal(_crewSource.Id, sourceLine.BudgetCodeId);
        Assert.False(sourceLine.NeedsJustification);
    }

    [Fact]
    public async Task A_zero_amount_line_is_copied_as_is()
    {
        // Zero is a valid plan — "we intend to spend nothing here" is a decision worth carrying.
        Plan(_source, _crewSource.Id, amount: 0m);

        var result = await CopyAsync();

        Assert.Equal(1, result.Value.Copied);
        Assert.Equal(0m, Assert.Single(TargetLines()).AmountCad);
    }

    // --- Skip rules: no active code with that string in the target ---

    [Fact]
    public async Task A_source_line_whose_string_is_retired_in_the_target_is_skipped_not_failed()
    {
        // Mirrors the create handler's CodeRetired guard, but as a skip: one dead code must not
        // cost the planner the other eleven lines.
        var old = AddCodeToBoth("ZBB-OLD-01", activeInTarget: false);
        Plan(_source, _crewSource.Id, "ZBB-CREW-01");
        Plan(_source, old.Source.Id, "ZBB-OLD-01");

        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetAllocationCopyResult(1, 0, 1, 2), result.Value);
        Assert.Equal("ZBB-CREW-01", Assert.Single(TargetLines()).Code);
    }

    [Fact]
    public async Task A_source_line_whose_string_the_target_chart_lacks_is_counted_as_no_active_code()
    {
        // The source's code is alive and well; the TARGET period simply has no code with that
        // string (nobody copied or created it there). Nothing to land on — skipped and counted.
        var onlyInSource = AddCode(_source, "ZBB-FUEL-01");
        Plan(_source, onlyInSource.Id, "ZBB-FUEL-01");

        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetAllocationCopyResult(0, 0, 1, 1), result.Value);
        Assert.Empty(TargetLines());
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_source_line_on_a_code_retired_in_the_source_is_still_copied_when_the_target_offers_it()
    {
        // Whether a code is on offer is a question about the period the item lands in. Retiring
        // OLD in Q3 says nothing about Q4, whose own OLD is active.
        var old = AddCodeToBoth("ZBB-OLD-01", activeInSource: false, activeInTarget: true);
        Plan(_source, old.Source.Id, "ZBB-OLD-01", 75m);

        var result = await CopyAsync();

        Assert.Equal(new BudgetAllocationCopyResult(1, 0, 0, 1), result.Value);
        Assert.Equal(old.Target.Id, Assert.Single(TargetLines()).BudgetCodeId);
    }

    [Fact]
    public async Task A_source_line_on_a_code_gone_from_both_charts_is_folded_into_the_same_count()
    {
        Plan(_source, Guid.NewGuid(), "ZBB-GONE-01");

        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetAllocationCopyResult(0, 0, 1, 1), result.Value);
        Assert.Empty(TargetLines());
    }

    // --- Skip rules: already planned ---

    [Fact]
    public async Task A_code_already_planned_in_the_target_is_skipped_and_never_overwritten()
    {
        // Overwriting would destroy a justification somebody already wrote, and adding to it would
        // make a second copy duplicate the plan. "Already planned" is judged on the TARGET's code
        // with the source item's string — a different id from the source's code.
        Plan(_source, _crewSource.Id, amount: 9999m, justification: "Last quarter's number.");
        Plan(_target, _crew.Id, amount: 1250m, justification: "This quarter, argued fresh.");

        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetAllocationCopyResult(0, 1, 0, 1), result.Value);
        var line = Assert.Single(TargetLines());
        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal("This quarter, argued fresh.", line.Justification);
        Assert.Null(line.ModifiedBy);
    }

    [Fact]
    public async Task A_source_period_with_no_lines_is_a_success_with_zeroes()
    {
        // 200, not an error: the button worked, there was simply nothing to bring across.
        var result = await CopyAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new BudgetAllocationCopyResult(0, 0, 0, 0), result.Value);
        Assert.Empty(_allocations.Allocations);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_copy_that_skips_every_line_saves_nothing()
    {
        var old = AddCodeToBoth("ZBB-OLD-01", activeInTarget: false);
        Plan(_source, old.Source.Id, "ZBB-OLD-01");
        Plan(_source, _crewSource.Id, "ZBB-CREW-01");
        Plan(_target, _crew.Id, "ZBB-CREW-01");

        var result = await CopyAsync();

        Assert.Equal(new BudgetAllocationCopyResult(0, 1, 1, 2), result.Value);
        Assert.Equal(0, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task Running_the_copy_twice_copies_nothing_the_second_time()
    {
        Plan(_source, _crewSource.Id);

        var first = await CopyAsync();
        var second = await CopyAsync();

        Assert.Equal(new BudgetAllocationCopyResult(1, 0, 0, 1), first.Value);
        Assert.Equal(new BudgetAllocationCopyResult(0, 1, 0, 1), second.Value);
        Assert.Single(TargetLines());
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    // --- The accounting invariant ---

    [Fact]
    public async Task The_counts_always_account_for_every_source_line()
    {
        // copied + skippedAlreadyPlanned + skippedRetiredCode == sourceLineCount. A skip nobody
        // counted reads as data loss.
        var fuel = AddCodeToBoth("ZBB-FUEL-01");
        var admin = AddCodeToBoth("ZBB-ADMIN-01");
        var old = AddCodeToBoth("ZBB-OLD-01", activeInTarget: false);
        var sourceOnly = AddCode(_source, "ZBB-NEW-01");

        Plan(_source, _crewSource.Id, "ZBB-CREW-01");     // copied
        Plan(_source, fuel.Source.Id, "ZBB-FUEL-01");     // copied
        Plan(_source, admin.Source.Id, "ZBB-ADMIN-01");   // already planned in the target
        Plan(_source, old.Source.Id, "ZBB-OLD-01");       // retired in the target
        Plan(_source, sourceOnly.Id, "ZBB-NEW-01");       // no such string in the target
        Plan(_source, Guid.NewGuid(), "ZBB-GONE-01");     // code gone everywhere
        Plan(_target, admin.Target.Id, "ZBB-ADMIN-01");

        var result = await CopyAsync();

        var counts = result.Value;
        Assert.Equal(6, counts.SourceLineCount);
        Assert.Equal(2, counts.Copied);
        Assert.Equal(1, counts.SkippedAlreadyPlanned);
        Assert.Equal(3, counts.SkippedRetiredCode);
        Assert.Equal(
            counts.SourceLineCount,
            counts.Copied + counts.SkippedAlreadyPlanned + counts.SkippedRetiredCode);
        Assert.Equal(3, TargetLines().Count);
    }

    [Fact]
    public async Task A_line_that_is_both_already_planned_and_retired_counts_once_as_already_planned()
    {
        // Precedence is documented on the handler: already-planned is tested first, because it is
        // the reason that actually protects something (an argued line in the target).
        var old = AddCodeToBoth("ZBB-OLD-01", activeInTarget: false);
        Plan(_source, old.Source.Id, "ZBB-OLD-01");
        Plan(_target, old.Target.Id, "ZBB-OLD-01");

        var result = await CopyAsync();

        Assert.Equal(new BudgetAllocationCopyResult(0, 1, 0, 1), result.Value);
    }

    // --- Many items per code ---

    [Fact]
    public async Task Every_item_of_a_code_is_copied_not_just_the_first()
    {
        // "Already planned" is a snapshot of the target BEFORE the copy: copying the first tires
        // item must not make the second one look already planned.
        Plan(_source, _crewSource.Id, amount: 1800m, justification: "Winter tires.");
        Plan(_source, _crewSource.Id, amount: 450m, justification: "Brake pads.");
        Plan(_source, _crewSource.Id, amount: 0m, justification: "Nothing on wipers this time.");

        var result = await CopyAsync();

        Assert.Equal(new BudgetAllocationCopyResult(3, 0, 0, 3), result.Value);
        var lines = TargetLines();
        Assert.Equal(3, lines.Count);
        Assert.Equal(2250m, lines.Sum(a => a.AmountCad));
        Assert.All(lines, a => Assert.True(a.NeedsJustification));
        Assert.All(lines, a => Assert.Equal(_crew.Id, a.BudgetCodeId));
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task One_item_already_on_a_code_in_the_target_skips_every_source_item_of_that_code()
    {
        var fuel = AddCodeToBoth("ZBB-FUEL-01");
        Plan(_source, _crewSource.Id, amount: 1800m);
        Plan(_source, _crewSource.Id, amount: 450m);
        Plan(_source, fuel.Source.Id, "ZBB-FUEL-01", 900m);
        Plan(_target, _crew.Id, amount: 99m, justification: "Started by hand.");

        var result = await CopyAsync();

        // Per ITEM: two crew items skipped, one fuel item copied — and the counts add up.
        Assert.Equal(new BudgetAllocationCopyResult(1, 2, 0, 3), result.Value);
        var lines = TargetLines();
        Assert.Equal(2, lines.Count);
        Assert.Equal(99m, Assert.Single(lines, a => a.BudgetCodeId == _crew.Id).AmountCad);
        Assert.Equal(900m, Assert.Single(lines, a => a.BudgetCodeId == fuel.Target.Id).AmountCad);
    }

    [Fact]
    public async Task Running_the_copy_twice_with_many_items_per_code_never_duplicates()
    {
        var fuel = AddCodeToBoth("ZBB-FUEL-01");
        var old = AddCodeToBoth("ZBB-OLD-01", activeInTarget: false);
        Plan(_source, _crewSource.Id, amount: 1800m);
        Plan(_source, _crewSource.Id, amount: 450m);
        Plan(_source, fuel.Source.Id, "ZBB-FUEL-01", 900m);
        Plan(_source, old.Source.Id, "ZBB-OLD-01", 10m);

        var first = await CopyAsync();
        var second = await CopyAsync();

        Assert.Equal(new BudgetAllocationCopyResult(3, 0, 1, 4), first.Value);
        // Second run: every live-code item is now already planned; the retired one stays retired.
        Assert.Equal(new BudgetAllocationCopyResult(0, 3, 1, 4), second.Value);
        foreach (var counts in new[] { first.Value, second.Value })
        {
            Assert.Equal(counts.SourceLineCount, counts.Copied + counts.SkippedAlreadyPlanned + counts.SkippedRetiredCode);
        }

        Assert.Equal(3, TargetLines().Count);
        Assert.Equal(1, _allocations.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_copied_item_keeps_every_field_except_its_justification()
    {
        var source = TestBudgeting.CreateAllocation(
            _source.Id, _crewSource.Id, _crewSource.Code, actorId: TestBudgeting.ActorId,
            details: TestBudgeting.Item(
                title: "Crew rotations",
                amount: null,
                quantity: 26m,
                unitCost: 72.5m,
                unit: "rotation",
                justification: "Last quarter's argument.",
                spendType: BudgetSpendType.Capital,
                recurrence: BudgetRecurrence.Recurring,
                vendor: "Alamos",
                tags: ["crew"],
                priority: BudgetItemPriority.MustHave,
                assumptions: "Two a week.",
                consequence: "Crew drives themselves."));
        _allocations.Add(source);

        await CopyAsync();

        var copy = Assert.Single(TargetLines());
        Assert.Equal(_crew.Id, copy.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", copy.Code);
        Assert.Equal("Crew rotations", copy.Title);
        Assert.Equal(1885m, copy.AmountCad);
        Assert.Equal(26m, copy.Quantity);
        Assert.Equal(72.5m, copy.UnitCostCad);
        Assert.Equal("rotation", copy.Unit);
        Assert.Equal(BudgetSpendType.Capital, copy.SpendType);
        Assert.Equal(BudgetRecurrence.Recurring, copy.Recurrence);
        Assert.Equal("Alamos", copy.Vendor);
        Assert.Equal(["crew"], copy.Tags);
        Assert.Equal(BudgetItemPriority.MustHave, copy.Priority);
        Assert.Equal("Two a week.", copy.Assumptions);
        Assert.Equal("Crew drives themselves.", copy.ConsequenceIfUnfunded);
        Assert.Equal(string.Empty, copy.Justification);
    }

    // --- The point of the whole feature ---

    [Fact]
    public async Task Every_copied_line_arrives_with_no_justification_and_must_be_re_argued()
    {
        // This is what makes the copy zero-based rather than a plain duplicate: the amount comes
        // across as a starting position, the argument does not come across at all, and the update
        // handler refuses to save the line again until somebody writes one.
        var fuel = AddCodeToBoth("ZBB-FUEL-01");
        Plan(_source, _crewSource.Id, "ZBB-CREW-01", 1250m, "Two crew rotations a week.");
        Plan(_source, fuel.Source.Id, "ZBB-FUEL-01", 480.50m, "Diesel at last quarter's price.");

        var result = await CopyAsync();

        Assert.Equal(2, result.Value.Copied);
        var copied = TargetLines();
        Assert.Equal(2, copied.Count);
        Assert.All(copied, line =>
        {
            Assert.True(line.NeedsJustification);
            Assert.Equal(string.Empty, line.Justification);
        });

        // …and re-saving one with nothing but whitespace is still refused, by the same Validate
        // the very first line in a period goes through.
        var crewCopy = Assert.Single(TargetLines(), a => a.BudgetCodeId == _crew.Id);
        var updateHandler = new UpdateBudgetAllocationCommandHandler(_allocations, _periods, _codes);
        var resave = await updateHandler.Handle(
            new UpdateBudgetAllocationCommand(
                TestBudgeting.TenantId, _target.Id, crewCopy.Id, _crew.Id,
                TestBudgeting.Item(amount: 1250m, justification: "   "), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(resave.IsFailure);
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, resave.Error);
        Assert.True(crewCopy.NeedsJustification);

        // Argue it properly and it saves, and stops needing one.
        var argued = await updateHandler.Handle(
            new UpdateBudgetAllocationCommand(
                TestBudgeting.TenantId, _target.Id, crewCopy.Id, _crew.Id,
                TestBudgeting.Item(amount: 1250m, justification: "Still two rotations, re-checked against the new contract."),
                TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(argued.IsSuccess);
        var line = Assert.Single(TargetLines(), a => a.BudgetCodeId == _crew.Id);
        Assert.Same(crewCopy, line);
        Assert.False(line.NeedsJustification);
        // The other copied line is untouched — one argued, one still owed.
        Assert.True(Assert.Single(TargetLines(), a => a.BudgetCodeId == fuel.Target.Id).NeedsJustification);
    }
}
