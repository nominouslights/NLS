using NorthernLink.Budgeting.Application.CostCentres;
using NorthernLink.Budgeting.Application.Periods;
using Xunit;
using static NorthernLink.Budgeting.Application.CostCentres.CostCentrePlannedRollup;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The planned per-cost-centre rollup: expense only, attributed by the code's cost-centre string,
/// with a "No cost centre" bucket, and a total that always equals the period's planned expense.
/// </summary>
public class CostCentrePlannedRollupTests
{
    private static readonly Guid PeriodId = Guid.NewGuid();

    private static readonly Guid Owner = Guid.NewGuid();

    private static readonly RegisterEntry North = new(Guid.NewGuid(), "NORTH", "North region", true, null, Owner);
    private static readonly RegisterEntry Thompson = new(Guid.NewGuid(), "THOMPSON", "Thompson base", true, North.Id, null);
    private static readonly RegisterEntry Churchill = new(Guid.NewGuid(), "CHURCHILL", "Churchill base", false, null, null);
    private static readonly RegisterEntry Idle = new(Guid.NewGuid(), "IDLE", "Idle base", true, null, null);
    private static readonly RegisterEntry RetiredUnused = new(Guid.NewGuid(), "GONE", "Gone base", false, null, null);

    private static readonly IReadOnlyDictionary<Guid, UserDisplay> Users =
        new Dictionary<Guid, UserDisplay> { [Owner] = new("owner@northernlink.ca", "Pat Owner") };

    private static readonly PeriodCode FuelThompson = new(Guid.NewGuid(), "Expense", "THOMPSON");
    private static readonly PeriodCode TiresThompson = new(Guid.NewGuid(), "Expense", "THOMPSON");
    private static readonly PeriodCode RentChurchill = new(Guid.NewGuid(), "Expense", "CHURCHILL");
    private static readonly PeriodCode Admin = new(Guid.NewGuid(), "Expense", null);
    private static readonly PeriodCode Charter = new(Guid.NewGuid(), "Revenue", null);
    private static readonly PeriodCode Legacy = new(Guid.NewGuid(), "Expense", "LEGACY");

    private static CostCentreRollupResponse Build(IEnumerable<ItemAmount> items, params PeriodCode[] codes) =>
        CostCentrePlannedRollup.Build(
            PeriodId, items, codes, [North, Thompson, Churchill, Idle, RetiredUnused], Users);

    [Fact]
    public void Sums_each_cost_centres_expense_items_with_a_no_cost_centre_bucket()
    {
        var unresolvable = Guid.NewGuid();
        var items = new[]
        {
            new ItemAmount(FuelThompson.Id, 1000.10m),
            new ItemAmount(FuelThompson.Id, 200.05m),
            new ItemAmount(TiresThompson.Id, 300.00m),
            new ItemAmount(RentChurchill.Id, 2500.00m),
            new ItemAmount(Admin.Id, 75.25m),
            new ItemAmount(Charter.Id, 99999.99m), // revenue — excluded everywhere
            new ItemAmount(unresolvable, 10.00m),  // unresolvable code — Expense, no cost centre
        };

        var rollup = Build(items, FuelThompson, TiresThompson, RentChurchill, Admin, Charter);

        Assert.Equal(PeriodId, rollup.PeriodId);
        var thompson = rollup.CostCentres.Single(r => r.Code == "THOMPSON");
        Assert.Equal(1500.15m, thompson.PlannedCad);
        Assert.Equal(2, thompson.BudgetCodeCount);
        Assert.Equal(3, thompson.ItemCount);
        Assert.Equal(North.Id, thompson.ParentId);
        Assert.Equal("NORTH", thompson.ParentCode);

        Assert.Equal(2500.00m, rollup.CostCentres.Single(r => r.Code == "CHURCHILL").PlannedCad);

        Assert.Equal(85.25m, rollup.NoCostCentre.PlannedCad);
        Assert.Equal(1, rollup.NoCostCentre.BudgetCodeCount); // Admin; the revenue code is not counted
        Assert.Equal(2, rollup.NoCostCentre.ItemCount);

        Assert.Equal(1500.15m + 2500.00m + 85.25m, rollup.TotalPlannedExpenseCad);
    }

    [Fact]
    public void The_total_equals_the_periods_planned_expense()
    {
        // The invariant the dashboard relies on: this rollup and PeriodPlannedTotals classify every
        // item the same way, so the two never disagree.
        var unresolvable = Guid.NewGuid();
        var codes = new[] { FuelThompson, RentChurchill, Admin, Charter, Legacy };
        var items = new[]
        {
            new ItemAmount(FuelThompson.Id, 12.34m),
            new ItemAmount(RentChurchill.Id, 1.01m),
            new ItemAmount(Admin.Id, 3.03m),
            new ItemAmount(Charter.Id, 500m),
            new ItemAmount(Legacy.Id, 7.77m),
            new ItemAmount(unresolvable, 4.00m),
        };

        var rollup = Build(items, codes);
        var totals = PeriodPlannedTotals.Sum(
            items.Select(i => new PeriodPlannedTotals.ItemAmount(PeriodId, i.BudgetCodeId, i.AmountCad)),
            codes.Select(c => new PeriodPlannedTotals.CodeCategory(PeriodId, c.Id, c.Category)));

        Assert.Equal(totals[PeriodId].ExpenseCad, rollup.TotalPlannedExpenseCad);
        Assert.Equal(
            rollup.TotalPlannedExpenseCad,
            rollup.CostCentres.Sum(r => r.PlannedCad) + rollup.NoCostCentre.PlannedCad);
    }

    [Fact]
    public void Every_active_entry_is_listed_even_with_nothing_planned_and_unused_retired_ones_are_not()
    {
        var rollup = Build([], FuelThompson);

        var idle = rollup.CostCentres.Single(r => r.Code == "IDLE");
        Assert.Equal(0m, idle.PlannedCad);
        Assert.Equal(0, idle.BudgetCodeCount);
        Assert.Contains(rollup.CostCentres, r => r.Code == "NORTH");
        Assert.DoesNotContain(rollup.CostCentres, r => r.Code == "GONE");
        Assert.DoesNotContain(rollup.CostCentres, r => r.Code == "CHURCHILL");
    }

    [Fact]
    public void A_retired_entry_still_carried_by_this_periods_codes_is_listed_as_retired()
    {
        var rollup = Build([new ItemAmount(RentChurchill.Id, 50m)], RentChurchill);

        var churchill = rollup.CostCentres.Single(r => r.Code == "CHURCHILL");
        Assert.False(churchill.IsActive);
        Assert.Equal(50m, churchill.PlannedCad);
    }

    [Fact]
    public void Owner_display_is_resolved_with_name_and_email()
    {
        var north = Build([]).CostCentres.Single(r => r.Code == "NORTH");

        Assert.Equal(Owner, north.OwnerUserId);
        Assert.Equal("Pat Owner", north.OwnerName);
        Assert.Equal("owner@northernlink.ca", north.OwnerEmail);
    }

    [Fact]
    public void A_string_no_register_entry_matches_gets_its_own_row_rather_than_vanishing()
    {
        var rollup = Build([new ItemAmount(Legacy.Id, 7.77m)], Legacy);

        var legacy = rollup.CostCentres.Single(r => r.Code == "LEGACY");
        Assert.Null(legacy.CostCentreId);
        Assert.Equal("LEGACY", legacy.Name);
        Assert.Equal(7.77m, legacy.PlannedCad);
    }

    [Fact]
    public void Rows_are_ordered_by_code_ordinally()
    {
        var rollup = Build([], FuelThompson, RentChurchill);

        Assert.Equal(
            rollup.CostCentres.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal),
            rollup.CostCentres.Select(r => r.Code));
    }

    [Fact]
    public void An_empty_period_has_zero_everywhere()
    {
        var rollup = Build([]);

        Assert.Equal(0m, rollup.TotalPlannedExpenseCad);
        Assert.Equal(0m, rollup.NoCostCentre.PlannedCad);
        Assert.All(rollup.CostCentres, r => Assert.Equal(0m, r.PlannedCad));
    }
}
