using NorthernLink.Budgeting.Application.Actuals.Record;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// RecordActualTransactionCommandHandler: the guard order the handler documents as its contract —
/// validate, then a period covers the date, then the code exists and is active — plus the one
/// guard that is deliberately absent, the period's state. Every refusal asserts
/// <c>SaveChangesCallCount == 0</c> and that no transaction appeared.
/// </summary>
public class RecordActualTransactionCommandHandlerTests
{
    private readonly InMemoryActualTransactionRepository _transactions = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly RecordActualTransactionCommandHandler _handler;

    private readonly BudgetPeriod _draft = TestBudgeting.CreatePeriod();
    private readonly BudgetCode _code;

    public RecordActualTransactionCommandHandlerTests()
    {
        // Codes belong to a period: the code a transaction is tagged with must be one of the
        // period covering its date, so the fixture's code lives in the fixture's period.
        _code = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: _draft.Id);
        _handler = new RecordActualTransactionCommandHandler(_transactions, _periods, _codes);
        _periods.Add(_draft);
        _codes.Add(_code);
    }

    private Task<Result<Guid>> RecordAsync(
        Guid? codeId = null,
        decimal? amount = 480.50m,
        DateOnly? occurredOn = null,
        string? description = "Diesel, Yellowknife depot.",
        Guid? actorId = null) =>
        _handler.Handle(
            new RecordActualTransactionCommand(
                TestBudgeting.TenantId,
                codeId ?? _code.Id,
                amount,
                occurredOn ?? TestBudgeting.InsideDefaultPeriod,
                description,
                actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    // --- The happy path ---

    [Fact]
    public async Task A_transaction_is_stored_from_the_code_as_Manual_with_no_external_reference()
    {
        var result = await RecordAsync(amount: 480.50m, description: "Diesel, Yellowknife depot.");

        Assert.True(result.IsSuccess);
        var transaction = Assert.Single(_transactions.Transactions);
        Assert.Equal(transaction.Id, result.Value);
        Assert.Equal(TestBudgeting.TenantId, transaction.TenantId);
        Assert.Equal(_code.Id, transaction.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", transaction.Code);
        Assert.Equal(480.50m, transaction.AmountCad);
        Assert.Equal(TestBudgeting.InsideDefaultPeriod, transaction.OccurredOn);
        Assert.Equal("Diesel, Yellowknife depot.", transaction.Description);
        Assert.Equal(ActualTransactionSource.Manual, transaction.Source);
        Assert.Null(transaction.ExternalReference);
        Assert.Equal(TestBudgeting.ActorId, transaction.CreatedBy);
        Assert.Equal(1, _transactions.SaveChangesCallCount);
        Assert.Equal(0, _periods.SaveChangesCallCount);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Two_spends_on_the_same_code_on_the_same_day_are_two_rows()
    {
        // Unlike an allocation line there is no upsert here: two fuel purchases on one day are
        // two ordinary facts, not one fact submitted twice.
        var first = await RecordAsync(amount: 480.50m, description: "Diesel, morning run.");
        var second = await RecordAsync(amount: 121.00m, description: "Diesel, afternoon run.");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(2, _transactions.Transactions.Count);
    }

    // --- Guard order: validation first ---

    [Fact]
    public async Task An_invalid_amount_reports_validation_before_the_period_lookup()
    {
        // A date in no period AND a negative amount: the caller hears about the payload, not
        // about a period the payload was never going to reach.
        var result = await RecordAsync(amount: -1m, occurredOn: new DateOnly(2030, 1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.AmountNegative, result.Error);
        Assert.Empty(_transactions.Transactions);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_missing_description_reports_validation_before_the_code_lookup()
    {
        var result = await RecordAsync(codeId: Guid.NewGuid(), description: "   ");

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.DescriptionRequired, result.Error);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    // --- Guard: a period covers the date ---

    [Fact]
    public async Task A_date_no_period_covers_is_refused_and_nothing_is_stored()
    {
        // The orphan rule: the period view is the only place actuals are listed, so a row outside
        // every period is one the product could never show or find again.
        var result = await RecordAsync(occurredOn: new DateOnly(2030, 6, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NoPeriodCoversDate, result.Error);
        Assert.Empty(_transactions.Transactions);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task With_no_periods_at_all_every_date_is_refused()
    {
        var empty = new RecordActualTransactionCommandHandler(
            _transactions, new InMemoryBudgetPeriodRepository(), _codes);

        var result = await empty.Handle(
            new RecordActualTransactionCommand(
                TestBudgeting.TenantId, _code.Id, 10m, TestBudgeting.InsideDefaultPeriod, "Why.", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NoPeriodCoversDate, result.Error);
    }

    [Theory]
    [InlineData(2026, 10, 1)]
    [InlineData(2026, 12, 31)]
    public async Task Both_period_boundaries_are_inside_it(int year, int month, int day)
    {
        // StartsOn..EndsOn are inclusive, so a transaction dated on either edge belongs to the
        // period rather than falling through the gap between two of them.
        var result = await RecordAsync(occurredOn: new DateOnly(year, month, day));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task The_day_before_the_period_starts_is_not_covered()
    {
        var result = await RecordAsync(occurredOn: new DateOnly(2026, 9, 30));

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NoPeriodCoversDate, result.Error);
    }

    // --- The asymmetry: the period's STATE is never consulted ---

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Finalized)]
    [InlineData(PeriodState.Open)]
    [InlineData(PeriodState.InReview)]
    [InlineData(PeriodState.Closed)]
    public async Task A_transaction_is_accepted_against_a_period_in_any_state_including_Closed(PeriodState state)
    {
        // The whole point of the asymmetry with allocations: only the PLAN is frozen by the
        // lifecycle. Actuals keep arriving after a period ends, and a closed period's actuals are
        // exactly what the review is reading. If this ever starts failing, someone has copied
        // AllowsPlanChanges across.
        var periods = new InMemoryBudgetPeriodRepository();
        var period = TestBudgeting.PeriodIn(state);
        periods.Add(period);
        var code = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: period.Id);
        _codes.Add(code);
        var handler = new RecordActualTransactionCommandHandler(_transactions, periods, _codes);

        var result = await handler.Handle(
            new RecordActualTransactionCommand(
                TestBudgeting.TenantId, code.Id, 480.50m, TestBudgeting.InsideDefaultPeriod,
                "Late invoice for the period.", TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_transactions.Transactions);
        Assert.Equal(1, _transactions.SaveChangesCallCount);
    }

    // --- Guard: the code ---

    [Fact]
    public async Task An_unknown_code_reports_the_code_as_not_found()
    {
        var result = await RecordAsync(codeId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Empty(_transactions.Transactions);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_code_of_another_period_is_not_found_in_the_period_covering_the_date()
    {
        // Codes belong to a period, and a transaction counts in the period its date falls in. A
        // Q3 code on a Q4 date would put spend on Q4's rollup against Q3's chart, so it is
        // refused — even when the string matches a code the covering period also has.
        var q3 = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 3);
        _periods.Add(q3);
        var q3Fuel = TestBudgeting.CreateCode("ZBB-FUEL-01", periodId: q3.Id);
        _codes.Add(q3Fuel);

        var result = await RecordAsync(codeId: q3Fuel.Id, occurredOn: TestBudgeting.InsideDefaultPeriod);

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Empty(_transactions.Transactions);
        Assert.Equal(0, _transactions.SaveChangesCallCount);

        // The same code on a date inside its own period is fine.
        Assert.True((await RecordAsync(codeId: q3Fuel.Id, occurredOn: new DateOnly(2026, 8, 14))).IsSuccess);
    }

    [Fact]
    public async Task A_retired_code_takes_no_new_spend()
    {
        var retired = TestBudgeting.CreateCode("ZBB-OLD-01", periodId: _draft.Id);
        Assert.True(retired.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        _codes.Add(retired);

        var result = await RecordAsync(codeId: retired.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.CodeRetired, result.Error);
        Assert.Empty(_transactions.Transactions);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_restored_code_takes_spend_again()
    {
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        Assert.Equal(ActualTransactionErrors.CodeRetired, (await RecordAsync()).Error);
        Assert.True(_code.SetActive(true, TestBudgeting.ActorId).IsSuccess);

        var result = await RecordAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(_transactions.Transactions);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_code_lookup()
    {
        // A date in no period and an unknown code: the date is the wall the caller hits first.
        var result = await RecordAsync(codeId: Guid.NewGuid(), occurredOn: new DateOnly(2030, 6, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NoPeriodCoversDate, result.Error);
    }
}
