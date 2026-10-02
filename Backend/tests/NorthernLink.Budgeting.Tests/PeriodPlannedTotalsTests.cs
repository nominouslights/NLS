using NorthernLink.Budgeting.Application.Periods;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// PeriodPlannedTotals — the rule behind every period's planned revenue / expense. Codes belong to
/// a period, so an item is classified by <em>its own period's</em> code: re-classifying FUEL in
/// one period must move that period's totals and no other's.
/// </summary>
public class PeriodPlannedTotalsTests
{
    private static readonly Guid Q3 = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    private static readonly Guid Q4 = Guid.Parse("a1000000-0000-0000-0000-000000000004");

    private static PeriodPlannedTotals.ItemAmount Item(Guid period, Guid code, decimal amount) => new(period, code, amount);

    private static PeriodPlannedTotals.CodeCategory Code(Guid period, Guid code, string category) => new(period, code, category);

    [Fact]
    public void Each_period_sums_its_own_items_by_its_own_codes_category()
    {
        // FUEL is Expense in Q3 but was re-classified Revenue in Q4 (a contrived edit, but exactly
        // the case per-period charts allow). Each period's totals follow its own chart.
        var fuelQ3 = Guid.NewGuid();
        var fuelQ4 = Guid.NewGuid();
        var crewQ4 = Guid.NewGuid();

        var totals = PeriodPlannedTotals.Sum(
            [Item(Q3, fuelQ3, 100m), Item(Q3, fuelQ3, 50m), Item(Q4, fuelQ4, 70m), Item(Q4, crewQ4, 1000m)],
            [Code(Q3, fuelQ3, "Expense"), Code(Q4, fuelQ4, "Revenue"), Code(Q4, crewQ4, "Revenue")]);

        Assert.Equal(new PeriodPlannedTotals.Totals(0m, 150m), totals[Q3]);
        Assert.Equal(new PeriodPlannedTotals.Totals(1070m, 0m), totals[Q4]);
    }

    [Fact]
    public void An_item_is_never_classified_by_another_periods_code()
    {
        // The item in Q4 names a code id that exists only in Q3's chart (data that should not
        // exist). It is not borrowed across periods: unresolved → Expense, like the item list.
        var revenueInQ3 = Guid.NewGuid();

        var totals = PeriodPlannedTotals.Sum(
            [Item(Q4, revenueInQ3, 40m)],
            [Code(Q3, revenueInQ3, "Revenue")]);

        Assert.Equal(new PeriodPlannedTotals.Totals(0m, 40m), totals[Q4]);
    }

    [Fact]
    public void An_item_whose_code_is_missing_counts_as_expense()
    {
        var totals = PeriodPlannedTotals.Sum([Item(Q3, Guid.NewGuid(), 12.34m)], []);

        Assert.Equal(new PeriodPlannedTotals.Totals(0m, 12.34m), totals[Q3]);
    }

    [Fact]
    public void A_period_with_no_items_is_absent_and_reads_as_zero()
    {
        var totals = PeriodPlannedTotals.Sum([], [Code(Q3, Guid.NewGuid(), "Revenue")]);

        Assert.False(totals.ContainsKey(Q3));
        Assert.Equal(PeriodPlannedTotals.Totals.Zero, totals.GetValueOrDefault(Q3, PeriodPlannedTotals.Totals.Zero));
    }
}
