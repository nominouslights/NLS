using NorthernLink.Budgeting.Application.Periods;
using NorthernLink.Budgeting.Application.Vendors;
using Xunit;
using static NorthernLink.Budgeting.Application.Vendors.VendorPlannedRollup;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The planned per-vendor rollup: expense only, attributed by each item's dollar shares, with a
/// "No vendor" bucket for expense items without shares, and a total that always equals the
/// period's planned expense.
/// </summary>
public class VendorPlannedRollupTests
{
    private static readonly Guid PeriodId = Guid.NewGuid();

    private static readonly RegisterEntry KalTire = new(Guid.NewGuid(), "Kal Tire Thompson", true);
    private static readonly RegisterEntry Petro = new(Guid.NewGuid(), "petro-Canada", true);
    private static readonly RegisterEntry OldGarage = new(Guid.NewGuid(), "Old Garage", false);
    private static readonly RegisterEntry Idle = new(Guid.NewGuid(), "Idle Supplier", true);

    private static readonly PeriodCode Fleet = new(Guid.NewGuid(), "Expense");
    private static readonly PeriodCode Fuel = new(Guid.NewGuid(), "Expense");
    private static readonly PeriodCode Charter = new(Guid.NewGuid(), "Revenue");

    private static Item ItemOn(PeriodCode code, decimal amount, params (RegisterEntry Vendor, decimal Amount)[] shares) =>
        new(code.Id, amount, shares.Select(s => new Share(s.Vendor.Id, s.Amount)).ToList());

    private static VendorRollupResponse Build(IEnumerable<Item> items, params PeriodCode[] codes) =>
        VendorPlannedRollup.Build(PeriodId, items, codes, [KalTire, Petro, OldGarage, Idle]);

    [Fact]
    public void Sums_each_vendors_shares_with_a_no_vendor_bucket()
    {
        var items = new[]
        {
            ItemOn(Fleet, 1000.10m, (KalTire, 600.05m), (Petro, 400.05m)),
            ItemOn(Fuel, 250.25m, (Petro, 250.25m)),
            ItemOn(Fleet, 75.50m),                                 // no shares → No vendor
            ItemOn(Charter, 99999.99m),                            // revenue — excluded everywhere
            ItemOn(new PeriodCode(Guid.NewGuid(), "x"), 10.00m),   // unresolvable code → Expense, No vendor
        };

        var rollup = Build(items, Fleet, Fuel, Charter);

        Assert.Equal(PeriodId, rollup.PeriodId);
        var petro = rollup.Vendors.Single(r => r.VendorId == Petro.Id);
        Assert.Equal(650.30m, petro.PlannedCad);
        Assert.Equal(2, petro.ItemCount);
        Assert.Equal("petro-Canada", petro.Name);
        Assert.True(petro.IsActive);

        var kal = rollup.Vendors.Single(r => r.VendorId == KalTire.Id);
        Assert.Equal(600.05m, kal.PlannedCad);
        Assert.Equal(1, kal.ItemCount);

        Assert.Equal(85.50m, rollup.NoVendor.PlannedCad);
        Assert.Equal(2, rollup.NoVendor.ItemCount);

        Assert.Equal(1000.10m + 250.25m + 75.50m + 10.00m, rollup.TotalPlannedExpenseCad);
    }

    [Fact]
    public void The_total_equals_the_periods_planned_expense()
    {
        var unresolvable = Guid.NewGuid();
        var codes = new[] { Fleet, Fuel, Charter };
        var items = new[]
        {
            ItemOn(Fleet, 12.34m, (KalTire, 12.00m), (OldGarage, 0.34m)),
            ItemOn(Fuel, 1.01m),
            ItemOn(Charter, 500m),
            new Item(unresolvable, 4.00m, []),
            ItemOn(Fuel, 0m),
        };

        var rollup = Build(items, codes);
        var totals = PeriodPlannedTotals.Sum(
            items.Select(i => new PeriodPlannedTotals.ItemAmount(PeriodId, i.BudgetCodeId, i.AmountCad)),
            codes.Select(c => new PeriodPlannedTotals.CodeCategory(PeriodId, c.Id, c.Category)));

        Assert.Equal(totals[PeriodId].ExpenseCad, rollup.TotalPlannedExpenseCad);
        Assert.Equal(
            rollup.TotalPlannedExpenseCad,
            rollup.Vendors.Sum(r => r.PlannedCad) + rollup.NoVendor.PlannedCad);
    }

    [Fact]
    public void Only_vendors_with_shares_are_listed_retired_ones_included_ordered_by_name()
    {
        var rollup = Build(
            [ItemOn(Fleet, 30m, (Petro, 10m), (OldGarage, 10m), (KalTire, 10m))],
            Fleet);

        Assert.DoesNotContain(rollup.Vendors, r => r.VendorId == Idle.Id);
        Assert.False(rollup.Vendors.Single(r => r.VendorId == OldGarage.Id).IsActive);
        // Case-insensitive name order: "Kal…", "Old…", "petro…".
        Assert.Equal([KalTire.Id, OldGarage.Id, Petro.Id], rollup.Vendors.Select(r => r.VendorId).ToArray());
    }

    [Fact]
    public void A_share_on_a_vendor_without_a_read_row_is_still_counted_with_a_null_name()
    {
        var unknown = Guid.NewGuid();
        var rollup = Build([new Item(Fleet.Id, 20m, [new Share(unknown, 20m)])], Fleet);

        var row = Assert.Single(rollup.Vendors);
        Assert.Equal(unknown, row.VendorId);
        Assert.Null(row.Name);
        Assert.False(row.IsActive);
        Assert.Equal(20m, rollup.TotalPlannedExpenseCad);
    }

    [Fact]
    public void Shares_that_disagree_with_their_item_keep_the_total_whole_through_no_vendor()
    {
        // Impossible through the API (the domain refuses it), but a hand-written row must not
        // break "rows + No vendor = planned expense".
        var rollup = Build([ItemOn(Fleet, 100m, (KalTire, 90m))], Fleet);

        Assert.Equal(100m, rollup.TotalPlannedExpenseCad);
        Assert.Equal(10m, rollup.NoVendor.PlannedCad);
        Assert.Equal(0, rollup.NoVendor.ItemCount);
        Assert.Equal(100m, rollup.Vendors.Sum(r => r.PlannedCad) + rollup.NoVendor.PlannedCad);
    }

    [Fact]
    public void An_empty_period_is_all_zeroes()
    {
        var rollup = Build([], Fleet);

        Assert.Empty(rollup.Vendors);
        Assert.Equal(0m, rollup.NoVendor.PlannedCad);
        Assert.Equal(0, rollup.NoVendor.ItemCount);
        Assert.Equal(0m, rollup.TotalPlannedExpenseCad);
    }
}
