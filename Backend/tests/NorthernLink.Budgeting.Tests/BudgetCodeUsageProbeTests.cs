using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// BudgetCodeUsageProbe: a code counts as used when any item <b>of its own period</b>, or any
/// recorded transaction <b>dated inside that period</b>, carries its id <em>or</em> its string.
/// The string half is why the probe takes both — a code deleted and recreated under the same
/// string has a new id, and its old rows must still count. The period half comes with per-period
/// charts: the string repeats across periods, so Q3's FUEL items or spend must not pin Q4's unused
/// FUEL. The allocations-or-actuals half: spend against a code pins it exactly as a plan does, or
/// a hard delete would leave figures on a ledger screen resolving to nothing. The last tests wire
/// the real probe into
/// <see cref="DeleteBudgetCodeCommandHandler"/> so the refusal is proven end to end, not only
/// through <see cref="StubBudgetCodeUsageProbe"/>.
/// </summary>
public class BudgetCodeUsageProbeTests
{
    private static readonly Guid PeriodId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryActualTransactionRepository _actuals = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly BudgetCodeUsageProbe _probe;

    public BudgetCodeUsageProbeTests()
    {
        _probe = new BudgetCodeUsageProbe(_allocations, _actuals, _periods);
    }

    [Fact]
    public async Task With_no_lines_anywhere_nothing_is_referenced()
    {
        Assert.False(await _probe.IsReferencedAsync(PeriodId, Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task A_line_carrying_the_id_makes_the_code_referenced()
    {
        var codeId = Guid.NewGuid();
        _allocations.Add(TestBudgeting.CreateAllocation(PeriodId, codeId, "ZBB-CREW-01"));

        // Probed with a string that matches nothing, so only the id can answer.
        Assert.True(await _probe.IsReferencedAsync(PeriodId, codeId, "ZBB-SOMETHING-ELSE"));
    }

    [Fact]
    public async Task A_line_carrying_only_the_string_makes_a_recreated_code_referenced()
    {
        // The line was created against an earlier code row; the code was deleted and recreated
        // under the same string with a fresh id. The old line still pins the string.
        _allocations.Add(TestBudgeting.CreateAllocation(PeriodId, Guid.NewGuid(), "ZBB-CREW-01"));

        Assert.True(await _probe.IsReferencedAsync(PeriodId, Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task A_line_matching_neither_the_id_nor_the_string_does_not_count()
    {
        _allocations.Add(TestBudgeting.CreateAllocation(PeriodId, Guid.NewGuid(), "ZBB-FUEL-01"));

        Assert.False(await _probe.IsReferencedAsync(PeriodId, Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task The_string_match_is_exact_like_the_database_column()
    {
        // Codes are normalized to upper case on the way in, so a case-different string is a
        // different code; the probe must not be more forgiving than the varchar equality it
        // stands in for, or it would refuse a delete on a code nothing references.
        _allocations.Add(TestBudgeting.CreateAllocation(PeriodId, Guid.NewGuid(), "ZBB-CREW-01"));

        Assert.False(await _probe.IsReferencedAsync(PeriodId, Guid.NewGuid(), "zbb-crew-01"));
    }

    [Fact]
    public async Task Another_periods_items_with_the_same_string_do_not_count()
    {
        // Q3 planned against ITS ZBB-CREW-01. Q4's ZBB-CREW-01 is another row that nothing in Q4
        // references — deleting it must not be refused because of Q3.
        var otherPeriod = Guid.NewGuid();
        _allocations.Add(TestBudgeting.CreateAllocation(otherPeriod, Guid.NewGuid(), "ZBB-CREW-01"));

        Assert.False(await _probe.IsReferencedAsync(PeriodId, Guid.NewGuid(), "ZBB-CREW-01"));
        Assert.True(await _probe.IsReferencedAsync(otherPeriod, Guid.NewGuid(), "ZBB-CREW-01"));
    }

    // --- The actuals half. A transaction stores no period, so these need a real one: its dates
    // bound which spend belongs to the code's period. (PeriodIn's default is 2026 Q4, which covers
    // TestBudgeting.InsideDefaultPeriod.) ---

    private BudgetPeriod AddPeriod(int year = 2026, int ordinal = 4)
    {
        var period = TestBudgeting.PeriodIn(PeriodState.Open, year: year, ordinal: ordinal);
        _periods.Add(period);
        return period;
    }

    [Fact]
    public async Task A_transaction_carrying_the_id_makes_the_code_referenced_with_no_lines_at_all()
    {
        // Spend pins a code exactly as a plan does: deleting the code would leave this figure on
        // the period's ledger resolving to nothing.
        var period = AddPeriod();
        var codeId = Guid.NewGuid();
        _actuals.Add(TestBudgeting.CreateActual(budgetCodeId: codeId, code: "ZBB-FUEL-01"));

        Assert.True(await _probe.IsReferencedAsync(period.Id, codeId, "ZBB-SOMETHING-ELSE"));
        Assert.Empty(_allocations.Allocations);
    }

    [Fact]
    public async Task A_transaction_carrying_only_the_string_makes_a_recreated_code_referenced()
    {
        var period = AddPeriod();
        _actuals.Add(TestBudgeting.CreateActual(budgetCodeId: Guid.NewGuid(), code: "ZBB-FUEL-01"));

        Assert.True(await _probe.IsReferencedAsync(period.Id, Guid.NewGuid(), "ZBB-FUEL-01"));
    }

    [Fact]
    public async Task A_transaction_matching_neither_the_id_nor_the_string_does_not_count()
    {
        var period = AddPeriod();
        _actuals.Add(TestBudgeting.CreateActual(budgetCodeId: Guid.NewGuid(), code: "ZBB-FUEL-01"));

        Assert.False(await _probe.IsReferencedAsync(period.Id, Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task Another_periods_spend_with_the_same_string_does_not_count()
    {
        // Q3 spent against ITS ZBB-FUEL-01 (dated in Q3). Q4's ZBB-FUEL-01 is another row, and
        // nothing dated in Q4 references it.
        var q3 = AddPeriod(ordinal: 3);
        var q4 = AddPeriod(ordinal: 4);
        _actuals.Add(TestBudgeting.CreateActual(
            budgetCodeId: Guid.NewGuid(), code: "ZBB-FUEL-01", occurredOn: new DateOnly(2026, 8, 14)));

        Assert.False(await _probe.IsReferencedAsync(q4.Id, Guid.NewGuid(), "ZBB-FUEL-01"));
        Assert.True(await _probe.IsReferencedAsync(q3.Id, Guid.NewGuid(), "ZBB-FUEL-01"));
    }

    [Fact]
    public async Task Spend_on_the_last_day_of_the_period_still_counts()
    {
        // Both ends are inclusive, like BudgetPeriod.Covers.
        var period = AddPeriod();
        var codeId = Guid.NewGuid();
        _actuals.Add(TestBudgeting.CreateActual(budgetCodeId: codeId, occurredOn: period.EndsOn));

        Assert.True(await _probe.IsReferencedAsync(period.Id, codeId, "ZBB-SOMETHING-ELSE"));
    }

    [Fact]
    public async Task Deleting_a_code_with_spend_but_no_plan_is_refused_as_in_use()
    {
        // The gap the allocation-only probe left: a code nobody ever planned against but money
        // was recorded on could be hard-deleted out from under its own transactions.
        var (codes, periods, period) = Chart();
        var probe = new BudgetCodeUsageProbe(_allocations, _actuals, periods);
        var code = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: period.Id);
        codes.Add(code);
        _actuals.Add(TestBudgeting.CreateActual(budgetCodeId: code.Id, code: code.Code));
        var handler = new DeleteBudgetCodeCommandHandler(codes, periods, probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, period.Id, code.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.InUse, result.Error);
        Assert.Single(codes.Codes);
        Assert.Equal(0, codes.SaveChangesCallCount);
    }

    // --- End to end through the delete handler ---

    private static (InMemoryBudgetCodeRepository Codes, InMemoryBudgetPeriodRepository Periods, BudgetPeriod Period) Chart()
    {
        var periods = new InMemoryBudgetPeriodRepository();
        var period = TestBudgeting.PeriodIn(PeriodState.Draft);
        periods.Add(period);
        return (new InMemoryBudgetCodeRepository(), periods, period);
    }

    [Fact]
    public async Task Deleting_a_code_with_a_line_is_refused_as_in_use_and_the_code_stays()
    {
        var (codes, periods, period) = Chart();
        var code = TestBudgeting.CreateCode("ZBB-CREW-01", periodId: period.Id);
        codes.Add(code);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, code.Id, code.Code));
        var handler = new DeleteBudgetCodeCommandHandler(codes, periods, _probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, period.Id, code.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.InUse, result.Error);
        Assert.Single(codes.Codes);
        Assert.Equal(0, codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deleting_a_recreated_code_whose_old_lines_survive_is_refused_too()
    {
        var (codes, periods, period) = Chart();
        var recreated = TestBudgeting.CreateCode("ZBB-CREW-01", periodId: period.Id);
        codes.Add(recreated);
        _allocations.Add(TestBudgeting.CreateAllocation(period.Id, Guid.NewGuid(), "ZBB-CREW-01"));
        var handler = new DeleteBudgetCodeCommandHandler(codes, periods, _probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, period.Id, recreated.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.InUse, result.Error);
        Assert.Single(codes.Codes);
    }

    [Fact]
    public async Task Deleting_a_code_no_line_of_its_period_references_succeeds_even_if_another_period_uses_the_string()
    {
        var (codes, periods, period) = Chart();
        var unused = TestBudgeting.CreateCode("ZBB-CREW-01", periodId: period.Id);
        codes.Add(unused);
        // Another period planned against its own ZBB-CREW-01.
        _allocations.Add(TestBudgeting.CreateAllocation(Guid.NewGuid(), Guid.NewGuid(), "ZBB-CREW-01"));
        var handler = new DeleteBudgetCodeCommandHandler(codes, periods, _probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, period.Id, unused.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(codes.Codes);
        Assert.Equal(1, codes.SaveChangesCallCount);
    }
}
