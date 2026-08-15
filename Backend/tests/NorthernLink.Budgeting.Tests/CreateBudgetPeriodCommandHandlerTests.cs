using NorthernLink.Budgeting.Application.Periods.Create;
using NorthernLink.Budgeting.Domain.Periods;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>CreateBudgetPeriodCommandHandler: validation order, overlap enforcement, persistence.</summary>
public class CreateBudgetPeriodCommandHandlerTests
{
    private readonly InMemoryBudgetPeriodRepository _repository = new();
    private readonly CreateBudgetPeriodCommandHandler _handler;

    public CreateBudgetPeriodCommandHandlerTests()
    {
        _handler = new CreateBudgetPeriodCommandHandler(_repository);
    }

    [Fact]
    public async Task Creates_a_period_and_saves_once()
    {
        var command = new CreateBudgetPeriodCommand(
            TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_repository.Periods);
        Assert.Equal(stored.Id, result.Value);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_exact_duplicate_is_rejected_as_overlapping()
    {
        _repository.Add(TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4));

        var result = await _handler.Handle(
            new CreateBudgetPeriodCommand(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.Overlapping, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_month_inside_an_existing_quarter_is_rejected_as_overlapping()
    {
        _repository.Add(TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4));

        var result = await _handler.Handle(
            new CreateBudgetPeriodCommand(TestBudgeting.TenantId, PeriodGranularity.Month, 2026, 11),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.Overlapping, result.Error);
    }

    [Fact]
    public async Task An_adjacent_quarter_succeeds()
    {
        _repository.Add(TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 3));

        var result = await _handler.Handle(
            new CreateBudgetPeriodCommand(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.Periods.Count);
    }

    [Fact]
    public async Task An_invalid_ordinal_reports_validation_not_conflict()
    {
        // Domain validation runs before the overlap check, so a bad ordinal never
        // reaches Overlapping even when other periods exist.
        _repository.Add(TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4));

        var result = await _handler.Handle(
            new CreateBudgetPeriodCommand(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 13),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.OrdinalOutOfRange, result.Error);
    }
}
