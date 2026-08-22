using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Periods.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>BudgetPeriod factory validation, date/label derivation, and overlap semantics.</summary>
public class BudgetPeriodTests
{
    [Fact]
    public void Create_always_starts_in_Draft()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(PeriodState.Draft, result.Value.State);
    }

    [Fact]
    public void Quarter_derives_inclusive_dates_and_fiscal_label()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4);

        Assert.True(result.IsSuccess);
        var period = result.Value;
        Assert.Equal(new DateOnly(2026, 10, 1), period.StartsOn);
        Assert.Equal(new DateOnly(2026, 12, 31), period.EndsOn);
        Assert.Equal("FY2026 Q4", period.Label);
    }

    [Fact]
    public void Month_derives_inclusive_dates_and_month_name_label()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Month, 2026, 3);

        Assert.True(result.IsSuccess);
        var period = result.Value;
        Assert.Equal(new DateOnly(2026, 3, 1), period.StartsOn);
        Assert.Equal(new DateOnly(2026, 3, 31), period.EndsOn);
        Assert.Equal("March 2026", period.Label);
    }

    [Fact]
    public void February_in_a_leap_year_ends_on_the_29th()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Month, 2028, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2028, 2, 29), result.Value.EndsOn);
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(2101)]
    public void Year_outside_2020_to_2100_is_rejected(int year)
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, year, 1);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.YearOutOfRange, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Quarter_ordinal_outside_1_to_4_is_rejected(int ordinal)
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, ordinal);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.OrdinalOutOfRange, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Month_ordinal_outside_1_to_12_is_rejected(int ordinal)
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Month, 2026, ordinal);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.OrdinalOutOfRange, result.Error);
    }

    [Fact]
    public void Create_raises_the_created_event()
    {
        var period = TestBudgeting.CreatePeriod();

        var created = Assert.Single(period.DomainEvents.OfType<BudgetPeriodCreatedDomainEvent>());
        Assert.Equal(period.Id, created.PeriodId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
    }

    [Fact]
    public void Overlaps_counts_a_same_day_boundary()
    {
        // Q4 2026 starts Oct 1 — a range ending exactly Oct 1 overlaps.
        var period = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4);

        Assert.True(period.Overlaps(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 1)));
        Assert.False(period.Overlaps(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void A_month_inside_an_existing_quarter_overlaps()
    {
        var quarter = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4);
        var month = TestBudgeting.CreatePeriod(PeriodGranularity.Month, 2026, 11);

        Assert.True(quarter.Overlaps(month.StartsOn, month.EndsOn));
    }
}
