using NorthernLink.Budgeting.Application.Actuals;
using NorthernLink.Budgeting.Domain.Codes;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// BudgetRollupBuilder: the rollup's arithmetic, with nothing in it that knows where the numbers
/// came from. The load-bearing case is the full outer join — a code planned but not spent against
/// and a code spent against but never planned must both produce a row, and the second is the one
/// a planner opens the table to find.
/// </summary>
public class BudgetRollupBuilderTests
{
    private static readonly Guid Fuel = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Crew = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid Charter = Guid.Parse("11111111-0000-0000-0000-000000000003");

    private static readonly Dictionary<Guid, BudgetRollupBuilder.CodeDescriptor> Chart = new()
    {
        [Fuel] = new("Fuel and lubricants", nameof(BudgetCodeCategory.Expense)),
        [Crew] = new("Crew wages", nameof(BudgetCodeCategory.Expense)),
        [Charter] = new("Charter revenue", nameof(BudgetCodeCategory.Revenue)),
    };

    private static BudgetRollupBuilder.CodeAmount Amount(Guid codeId, string code, decimal amount) =>
        new(codeId, code, amount);

    [Fact]
    public void A_code_with_a_plan_and_spend_produces_one_row_with_both_and_the_delta()
    {
        var rows = BudgetRollupBuilder.Build(
            [Amount(Fuel, "ZBB-FUEL-01", 1000m)],
            [Amount(Fuel, "ZBB-FUEL-01", 1250m)],
            Chart);

        var row = Assert.Single(rows);
        Assert.Equal(Fuel, row.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", row.Code);
        Assert.Equal("Fuel and lubricants", row.Name);
        Assert.Equal(nameof(BudgetCodeCategory.Expense), row.Category);
        Assert.Equal(1000m, row.PlannedCad);
        Assert.Equal(1250m, row.ActualCad);
        Assert.Equal(250m, row.DeltaCad);
    }

    [Fact]
    public void A_code_planned_but_never_spent_against_still_appears_with_zero_actual()
    {
        var rows = BudgetRollupBuilder.Build(
            [Amount(Crew, "ZBB-CREW-01", 8000m)],
            [],
            Chart);

        var row = Assert.Single(rows);
        Assert.Equal(8000m, row.PlannedCad);
        Assert.Equal(0m, row.ActualCad);
        Assert.Equal(-8000m, row.DeltaCad);
    }

    [Fact]
    public void A_code_spent_against_but_never_planned_still_appears_with_zero_planned()
    {
        // The row zero-based budgeting exists to surface: money going somewhere nobody argued
        // for. Dropping it would hide exactly the thing the table is opened to find.
        var rows = BudgetRollupBuilder.Build(
            [],
            [Amount(Fuel, "ZBB-FUEL-01", 640m)],
            Chart);

        var row = Assert.Single(rows);
        Assert.Equal(Fuel, row.BudgetCodeId);
        Assert.Equal(0m, row.PlannedCad);
        Assert.Equal(640m, row.ActualCad);
        Assert.Equal(640m, row.DeltaCad);
    }

    [Fact]
    public void Both_kinds_of_one_sided_row_appear_together_alongside_the_matched_ones()
    {
        var rows = BudgetRollupBuilder.Build(
            [Amount(Crew, "ZBB-CREW-01", 8000m), Amount(Charter, "ZBB-CHRT-01", 20000m)],
            [Amount(Charter, "ZBB-CHRT-01", 19500m), Amount(Fuel, "ZBB-FUEL-01", 640m)],
            Chart);

        Assert.Equal(3, rows.Count);
        var byCode = rows.ToDictionary(r => r.Code);
        Assert.Equal((8000m, 0m), (byCode["ZBB-CREW-01"].PlannedCad, byCode["ZBB-CREW-01"].ActualCad));
        Assert.Equal((20000m, 19500m), (byCode["ZBB-CHRT-01"].PlannedCad, byCode["ZBB-CHRT-01"].ActualCad));
        Assert.Equal((0m, 640m), (byCode["ZBB-FUEL-01"].PlannedCad, byCode["ZBB-FUEL-01"].ActualCad));
    }

    [Fact]
    public void Many_transactions_on_one_code_sum_into_a_single_row()
    {
        var rows = BudgetRollupBuilder.Build(
            [Amount(Fuel, "ZBB-FUEL-01", 1000m)],
            [
                Amount(Fuel, "ZBB-FUEL-01", 480.50m),
                Amount(Fuel, "ZBB-FUEL-01", 121.00m),
                Amount(Fuel, "ZBB-FUEL-01", 38.50m),
            ],
            Chart);

        var row = Assert.Single(rows);
        Assert.Equal(640.00m, row.ActualCad);
        Assert.Equal(-360.00m, row.DeltaCad);
    }

    [Fact]
    public void Rows_come_back_ordered_by_code_string()
    {
        var rows = BudgetRollupBuilder.Build(
            [Amount(Fuel, "ZBB-FUEL-01", 1m), Amount(Charter, "ZBB-CHRT-01", 1m)],
            [Amount(Crew, "ZBB-CREW-01", 1m)],
            Chart);

        Assert.Equal(["ZBB-CHRT-01", "ZBB-CREW-01", "ZBB-FUEL-01"], rows.Select(r => r.Code));
    }

    [Fact]
    public void Two_codes_sharing_a_string_are_not_merged_because_the_grouping_key_is_the_id()
    {
        // A code deleted and recreated under the same string is a different code with a different
        // id; merging the old code's spend into the new code's plan would invent a comparison
        // nobody made.
        var recreated = Guid.Parse("11111111-0000-0000-0000-00000000000f");

        var rows = BudgetRollupBuilder.Build(
            [Amount(recreated, "ZBB-FUEL-01", 1000m)],
            [Amount(Fuel, "ZBB-FUEL-01", 640m)],
            Chart);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("ZBB-FUEL-01", row.Code));
        Assert.Contains(rows, row => row.PlannedCad == 1000m && row.ActualCad == 0m);
        Assert.Contains(rows, row => row.PlannedCad == 0m && row.ActualCad == 640m);
    }

    [Fact]
    public void A_code_the_chart_no_longer_has_still_produces_a_row_labelled_by_its_own_string()
    {
        // Matches what the lines read does with the same situation, so the rollup and the lines
        // it is built from can never disagree about where a figure belongs.
        var unknown = Guid.Parse("11111111-0000-0000-0000-0000000000ff");

        var rows = BudgetRollupBuilder.Build(
            [],
            [Amount(unknown, "ZBB-GONE-01", 99m)],
            Chart);

        var row = Assert.Single(rows);
        Assert.Equal("ZBB-GONE-01", row.Name);
        Assert.Equal(nameof(BudgetCodeCategory.Expense), row.Category);
        Assert.Equal(99m, row.ActualCad);
    }

    [Fact]
    public void Nothing_planned_and_nothing_spent_is_an_empty_rollup_not_a_row_of_zeroes()
    {
        Assert.Empty(BudgetRollupBuilder.Build([], [], Chart));
    }

    [Fact]
    public void The_rollup_carries_no_variance_band_percentage_or_tax_field()
    {
        // Two pinned product rules in one assertion. The console owns the variance thresholds —
        // a server shipping its own would put two answers to one question in the product. And the
        // platform never computes tax, so there is nowhere here for a rate to appear.
        var properties = typeof(BudgetRollupRowResponse)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(
            ["BudgetCodeId", "Code", "Name", "Category", "PlannedCad", "ActualCad", "DeltaCad"],
            properties);
    }
}
