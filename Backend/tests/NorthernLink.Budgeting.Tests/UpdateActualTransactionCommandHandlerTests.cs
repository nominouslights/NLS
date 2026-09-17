using NorthernLink.Budgeting.Application.Actuals.Update;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// UpdateActualTransactionCommandHandler: the correction path. Same guards as recording plus the
/// row itself, and the same deliberate absence — the period's state is never consulted, so a
/// closed period's books stay correctable.
/// </summary>
public class UpdateActualTransactionCommandHandlerTests
{
    private readonly InMemoryActualTransactionRepository _transactions = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly UpdateActualTransactionCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _fuel = TestBudgeting.CreateCode("ZBB-FUEL-01");
    private readonly BudgetCode _maintenance = TestBudgeting.CreateCode("ZBB-MAINT-01");
    private readonly ActualTransaction _transaction;

    public UpdateActualTransactionCommandHandlerTests()
    {
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
        var q1 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(q1);

        var result = await UpdateAsync(occurredOn: new DateOnly(2027, 2, 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2027, 2, 3), _transaction.OccurredOn);
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
    public async Task A_row_whose_own_code_was_retired_since_cannot_be_corrected_in_place_either()
    {
        // Same call the allocation set handler makes: a correction is a new decision against a
        // code nobody offers any more, and the way out is to restore the code or move the row.
        Assert.True(_fuel.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await UpdateAsync(description: "Just fixing a typo.");

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.CodeRetired, result.Error);
        Assert.Equal("Diesel.", _transaction.Description);
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
        periods.Add(TestBudgeting.PeriodIn(state));
        var handler = new UpdateActualTransactionCommandHandler(_transactions, periods, _codes);

        var result = await handler.Handle(
            new UpdateActualTransactionCommand(
                TestBudgeting.TenantId, _transaction.Id, _fuel.Id, 512.00m,
                TestBudgeting.InsideDefaultPeriod, "Corrected during review.", TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(512.00m, _transaction.AmountCad);
        Assert.Equal(1, _transactions.SaveChangesCallCount);
    }
}
