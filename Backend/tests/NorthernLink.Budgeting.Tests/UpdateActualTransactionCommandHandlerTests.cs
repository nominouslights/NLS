using NorthernLink.Budgeting.Application.Actuals.Update;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// UpdateActualTransactionCommandHandler: the correction path. Same guards as recording plus the
/// row itself, and two deliberate narrowings in the same direction — the period's state is never
/// consulted, and a retired code blocks only a <em>move</em> onto it. Both say the same thing: a
/// fact must always be correctable, whatever has happened to the period or the code since.
/// </summary>
public class UpdateActualTransactionCommandHandlerTests
{
    private readonly InMemoryActualTransactionRepository _transactions = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly UpdateActualTransactionCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _fuel;
    private readonly BudgetCode _maintenance;
    private readonly ActualTransaction _transaction;

    public UpdateActualTransactionCommandHandlerTests()
    {
        // Codes belong to a period; the fixture's codes live in the period covering its date.
        _fuel = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: _draft.Id);
        _maintenance = TestBudgeting.CreateCode("ZBB-MAINT-01", periodId: _draft.Id);
        _handler = new UpdateActualTransactionCommandHandler(_transactions, _periods, _codes);
        _periods.Add(_draft);
        _codes.Add(_fuel);
        _codes.Add(_maintenance);
        _transaction = TestBudgeting.CreateActual(
            _fuel.Id, _fuel.Code, 480.50m, TestBudgeting.InsideDefaultPeriod, "Diesel.", actorId: TestBudgeting.ActorId);
        _transactions.Add(_transaction);
    }

    private Task<Result> UpdateAsync(
        Guid? transactionId = null,
        Guid? codeId = null,
        decimal? amount = 512.00m,
        DateOnly? occurredOn = null,
        string? description = "Corrected.",
        Guid? actorId = null) =>
        _handler.Handle(
            new UpdateActualTransactionCommand(
                TestBudgeting.TenantId,
                transactionId ?? _transaction.Id,
                codeId ?? _fuel.Id,
                amount,
                occurredOn ?? TestBudgeting.InsideDefaultPeriod,
                description,
                actorId),
            CancellationToken.None);

    [Fact]
    public async Task The_correction_is_applied_and_saved_once()
    {
        var editor = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var result = await UpdateAsync(amount: 512.00m, description: "Corrected after the invoice.", actorId: editor);

        Assert.True(result.IsSuccess);
        Assert.Equal(512.00m, _transaction.AmountCad);
        Assert.Equal("Corrected after the invoice.", _transaction.Description);
        Assert.Equal(editor, _transaction.ModifiedBy);
        Assert.Equal(TestBudgeting.ActorId, _transaction.CreatedBy);
        Assert.Equal(1, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_miscoded_expense_can_be_moved_to_another_code_and_the_string_moves_with_it()
    {
        var result = await UpdateAsync(codeId: _maintenance.Id, description: "Miscoded — this was maintenance.");

        Assert.True(result.IsSuccess);
        Assert.Equal(_maintenance.Id, _transaction.BudgetCodeId);
        Assert.Equal("ZBB-MAINT-01", _transaction.Code);
    }

    [Fact]
    public async Task Moving_the_date_is_how_a_transaction_moves_between_periods()
    {
        // Codes belong to a period, so a move between periods is also a re-tag onto the new
        // period's code. Keeping Q4's code on a Q1 date is refused: Q1's rollup would otherwise
        // carry spend against Q4's chart.
        var q1 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(q1);
        var q1Fuel = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: q1.Id);
        _codes.Add(q1Fuel);

        var kept = await UpdateAsync(occurredOn: new DateOnly(2027, 2, 3));
        Assert.Equal(BudgetCodeErrors.NotFound, kept.Error);
        Assert.Equal(TestBudgeting.InsideDefaultPeriod, _transaction.OccurredOn);

        var result = await UpdateAsync(codeId: q1Fuel.Id, occurredOn: new DateOnly(2027, 2, 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2027, 2, 3), _transaction.OccurredOn);
        Assert.Equal(q1Fuel.Id, _transaction.BudgetCodeId);
    }

    // --- Guards ---

    [Fact]
    public async Task An_invalid_amount_reports_validation_before_the_row_lookup()
    {
        var result = await UpdateAsync(transactionId: Guid.NewGuid(), amount: -1m);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.AmountNegative, result.Error);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_transaction_is_not_found()
    {
        var result = await UpdateAsync(transactionId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NotFound, result.Error);
        Assert.Equal(480.50m, _transaction.AmountCad);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_correction_onto_a_date_no_period_covers_is_refused_and_changes_nothing()
    {
        var result = await UpdateAsync(occurredOn: new DateOnly(2030, 6, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NoPeriodCoversDate, result.Error);
        Assert.Equal(TestBudgeting.InsideDefaultPeriod, _transaction.OccurredOn);
        Assert.Equal("Diesel.", _transaction.Description);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_code_reports_the_code_as_not_found()
    {
        var result = await UpdateAsync(codeId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_correction_onto_a_retired_code_is_refused_and_leaves_the_row_alone()
    {
        Assert.True(_maintenance.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await UpdateAsync(codeId: _maintenance.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.CodeRetired, result.Error);
        Assert.Equal(_fuel.Id, _transaction.BudgetCodeId);
        Assert.Equal("Diesel.", _transaction.Description);
        Assert.Null(_transaction.ModifiedBy);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_row_whose_own_code_was_retired_since_is_still_correctable_in_place()
    {
        // Narrower than the record path on purpose: a retired code must never stand between
        // anyone and fixing a fact. The edit does not move the transaction anywhere, so the
        // retirement is none of its business.
        Assert.True(_fuel.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await UpdateAsync(description: "Just fixing a typo.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Just fixing a typo.", _transaction.Description);
        Assert.Equal(_fuel.Id, _transaction.BudgetCodeId);
        Assert.Equal(1, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_amount_and_the_date_are_correctable_on_a_retired_code_too()
    {
        // The whole row stays correctable, not just its prose — a wrong figure on historical data
        // is the correction that matters most.
        Assert.True(_fuel.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await UpdateAsync(amount: 612.75m, occurredOn: new DateOnly(2026, 12, 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(612.75m, _transaction.AmountCad);
        Assert.Equal(new DateOnly(2026, 12, 2), _transaction.OccurredOn);
    }

    [Fact]
    public async Task Moving_a_row_off_a_retired_code_onto_a_live_one_is_allowed()
    {
        // The escape hatch the narrowing has to leave open: a row stranded on a retired code can
        // be re-pointed at a live one without anyone restoring the retired code first.
        Assert.True(_fuel.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await UpdateAsync(codeId: _maintenance.Id, description: "Miscoded.");

        Assert.True(result.IsSuccess);
        Assert.Equal(_maintenance.Id, _transaction.BudgetCodeId);
        Assert.Equal("ZBB-MAINT-01", _transaction.Code);
    }

    // --- The asymmetry ---

    [Theory]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_transaction_in_a_frozen_period_is_still_correctable(PeriodState state)
    {
        // Precisely when it matters: a review is the moment someone is looking closely enough to
        // find the error. Only the plan is frozen by the lifecycle.
        var periods = new InMemoryBudgetPeriodRepository();
        var period = TestBudgeting.PeriodIn(state);
        periods.Add(period);
        var fuel = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: period.Id);
        _codes.Add(fuel);
        var transaction = TestBudgeting.CreateActual(
            fuel.Id, fuel.Code, 480.50m, TestBudgeting.InsideDefaultPeriod, "Diesel.", actorId: TestBudgeting.ActorId);
        _transactions.Add(transaction);
        var handler = new UpdateActualTransactionCommandHandler(_transactions, periods, _codes);

        var result = await handler.Handle(
            new UpdateActualTransactionCommand(
                TestBudgeting.TenantId, transaction.Id, fuel.Id, 512.00m,
                TestBudgeting.InsideDefaultPeriod, "Corrected during review.", TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(512.00m, transaction.AmountCad);
        Assert.Equal(1, _transactions.SaveChangesCallCount);
    }
}
