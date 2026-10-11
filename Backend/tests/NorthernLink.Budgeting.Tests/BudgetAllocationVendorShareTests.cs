using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Budgeting.Tests.TestBudgeting;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The vendor-share rules on the aggregate: dollar shares on expense items that sum to the item's
/// amount exactly (cents included), no partial splits, each vendor once, none on revenue items, no
/// new free-text payer on expense items, an amount change the shares no longer sum to is refused
/// (never rebalanced), the "only on change" leniency, and what a copy carries.
/// </summary>
public class BudgetAllocationVendorShareTests
{
    private static readonly Guid PeriodId = Guid.NewGuid();
    private static readonly Guid CodeId = Guid.NewGuid();
    private static readonly Guid Fuel = Guid.NewGuid();
    private static readonly Guid Tires = Guid.NewGuid();
    private static readonly Guid Parts = Guid.NewGuid();

    private static Result<BudgetAllocation> CreateExpense(BudgetItemDetails details) =>
        BudgetAllocation.Create(TenantId, PeriodId, CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, details, ActorId);

    private static BudgetAllocation ValidExpense(decimal amount, params BudgetItemVendorShareDetails[] shares)
    {
        var result = CreateExpense(Item(amount: amount, vendorShares: shares));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private static Error ExpenseErrorOf(BudgetItemDetails details)
    {
        var result = CreateExpense(details);
        Assert.True(result.IsFailure);
        return result.Error;
    }

    // --- The sum rule ---

    [Fact]
    public void An_expense_item_splits_across_vendors_in_dollars_summing_exactly_to_the_amount()
    {
        var item = ValidExpense(1000.10m, Share(Fuel, 600.05m), Share(Tires, 400.05m));

        Assert.Equal(
            [(Fuel, 600.05m), (Tires, 400.05m)],
            item.VendorShares.Select(s => (s.VendorId, s.AmountCad)).ToArray());
    }

    [Fact]
    public void A_split_one_cent_short_is_refused_naming_both_totals()
    {
        var error = ExpenseErrorOf(Item(amount: 1000.10m, vendorShares: [Share(Fuel, 600.05m), Share(Tires, 400.04m)]));

        Assert.Equal("Budgeting.Allocation.VendorSharesMustSumToAmount", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Contains("1,000.09", error.Message);
        Assert.Contains("1,000.10", error.Message);
    }

    [Fact]
    public void A_partial_split_is_refused_any_share_means_the_whole_amount()
    {
        var error = ExpenseErrorOf(Item(amount: 500m, vendorShares: [Share(Fuel, 200m)]));

        Assert.Equal("Budgeting.Allocation.VendorSharesMustSumToAmount", error.Code);
    }

    [Fact]
    public void Shares_are_checked_against_the_computed_amount_of_a_built_up_item()
    {
        // 3 × 33.335 → unit cost rounds to 33.34 → amount 100.02, and that is the figure to cover.
        var item = CreateExpense(Item(
            amount: 999m, quantity: 3m, unitCost: 33.335m, unit: "tire",
            vendorShares: [Share(Tires, 50.01m), Share(Parts, 50.01m)]));

        Assert.True(item.IsSuccess);
        Assert.Equal(100.02m, item.Value.AmountCad);
    }

    [Fact]
    public void An_item_with_no_shares_is_valid_and_reads_as_no_vendor()
    {
        Assert.Empty(ValidExpense(250m).VendorShares);
        Assert.Empty(CreateExpense(Item(amount: 250m, vendorShares: [])).Value.VendorShares);
    }

    [Fact]
    public void A_zero_amount_item_cannot_carry_a_share()
    {
        Assert.Equal(
            BudgetAllocationErrors.VendorShareAmountInvalid,
            ExpenseErrorOf(Item(amount: 0m, vendorShares: [Share(Fuel, 0m)])));
        Assert.Equal(
            "Budgeting.Allocation.VendorSharesMustSumToAmount",
            ExpenseErrorOf(Item(amount: 0m, vendorShares: [Share(Fuel, 1m)])).Code);
    }

    // --- Each share ---

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("10.005")]
    [InlineData("1000000000.00")]
    public void A_share_must_be_positive_whole_cents_and_within_the_money_ceiling(string raw)
    {
        var amount = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(
            BudgetAllocationErrors.VendorShareAmountInvalid,
            ExpenseErrorOf(Item(amount: 10m, vendorShares: [Share(Fuel, amount)])));
    }

    [Fact]
    public void A_share_with_no_amount_is_invalid()
    {
        Assert.Equal(
            BudgetAllocationErrors.VendorShareAmountInvalid,
            ExpenseErrorOf(Item(amount: 10m, vendorShares: [Share(Fuel, null)])));
    }

    [Fact]
    public void A_share_must_name_a_vendor()
    {
        Assert.Equal(
            BudgetAllocationErrors.VendorShareVendorRequired,
            ExpenseErrorOf(Item(amount: 10m, vendorShares: [Share(null, 10m)])));
        Assert.Equal(
            BudgetAllocationErrors.VendorShareVendorRequired,
            ExpenseErrorOf(Item(amount: 10m, vendorShares: [Share(Guid.Empty, 10m)])));
    }

    [Fact]
    public void Each_vendor_appears_at_most_once()
    {
        Assert.Equal(
            BudgetAllocationErrors.DuplicateVendorShare,
            ExpenseErrorOf(Item(amount: 10m, vendorShares: [Share(Fuel, 5m), Share(Fuel, 5m)])));
    }

    [Fact]
    public void An_item_splits_across_at_most_twenty_vendors()
    {
        var twenty = Enumerable.Range(0, 20).Select(_ => Share(Guid.NewGuid(), 1m)).ToArray();
        Assert.True(CreateExpense(Item(amount: 20m, vendorShares: twenty)).IsSuccess);

        var twentyOne = Enumerable.Range(0, 21).Select(_ => Share(Guid.NewGuid(), 1m)).ToArray();
        Assert.Equal(
            BudgetAllocationErrors.TooManyVendorShares,
            ExpenseErrorOf(Item(amount: 21m, vendorShares: twentyOne)));
    }

    [Fact]
    public void Validate_checks_stated_shares_before_any_lookup()
    {
        // The handlers call Validate before touching the period, the code or the register, so a
        // wrong sum is a 400 even for an item whose period does not exist.
        Assert.Equal(
            "Budgeting.Allocation.VendorSharesMustSumToAmount",
            BudgetAllocation.Validate(Item(amount: 10m, vendorShares: [Share(Fuel, 9m)])).Error.Code);
        Assert.True(BudgetAllocation.Validate(Item(amount: 10m, vendorShares: [Share(Fuel, 10m)])).IsSuccess);
    }

    // --- Category rules ---

    [Fact]
    public void A_revenue_item_refuses_vendor_shares_but_keeps_its_payer()
    {
        var refused = BudgetAllocation.Create(
            TenantId, PeriodId, CodeId, "ZBB-CREW-01", BudgetCodeCategory.Revenue,
            Item(amount: 10m, vendorShares: [Share(Fuel, 10m)]), ActorId);

        Assert.Equal(BudgetAllocationErrors.VendorSharesNotAllowedForRevenue, refused.Error);

        var payer = BudgetAllocation.Create(
            TenantId, PeriodId, CodeId, "ZBB-CREW-01", BudgetCodeCategory.Revenue,
            Item(amount: 10m, vendor: "Alamos Gold", vendorShares: []), ActorId);

        Assert.True(payer.IsSuccess);
        Assert.Equal("Alamos Gold", payer.Value.Vendor);
        Assert.Empty(payer.Value.VendorShares);
    }

    [Fact]
    public void An_expense_item_refuses_new_free_text_vendor()
    {
        Assert.Equal(
            BudgetAllocationErrors.PayerNotAllowedForExpense,
            ExpenseErrorOf(Item(amount: 10m, vendor: "Kal Tire")));

        // Blank text normalizes to null and is fine.
        Assert.True(CreateExpense(Item(amount: 10m, vendor: "   ")).IsSuccess);
    }

    // --- Updates ---

    [Fact]
    public void Changing_the_amount_without_restating_the_shares_is_refused_never_rebalanced()
    {
        var item = ValidExpense(100m, Share(Fuel, 60m), Share(Tires, 40m));

        // Null shares = keep the current ones, which no longer sum to 120.
        var result = item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, Item(amount: 120m), ActorId);

        Assert.Equal("Budgeting.Allocation.VendorSharesMustSumToAmount", result.Error.Code);
        Assert.Contains("100.00", result.Error.Message);
        Assert.Contains("120.00", result.Error.Message);
        Assert.Equal(100m, item.AmountCad);
        Assert.Equal([60m, 40m], item.VendorShares.Select(s => s.AmountCad).ToArray());
    }

    [Fact]
    public void Changing_the_amount_with_restated_shares_saves()
    {
        var item = ValidExpense(100m, Share(Fuel, 60m), Share(Tires, 40m));

        var result = item.Update(
            CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense,
            Item(amount: 120m, vendorShares: [Share(Fuel, 70m), Share(Tires, 50m)]), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(120m, item.AmountCad);
        Assert.Equal([70m, 50m], item.VendorShares.Select(s => s.AmountCad).ToArray());
    }

    [Fact]
    public void Null_shares_keep_the_current_ones_and_an_empty_list_clears_them()
    {
        var item = ValidExpense(100m, Share(Fuel, 100m));

        Assert.True(item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, Item(amount: 100m, title: "Renamed"), ActorId).IsSuccess);
        Assert.Equal(Fuel, Assert.Single(item.VendorShares).VendorId);

        Assert.True(item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, Item(amount: 100m, vendorShares: []), ActorId).IsSuccess);
        Assert.Empty(item.VendorShares);
    }

    [Fact]
    public void An_item_whose_code_became_revenue_keeps_its_unchanged_shares_but_cannot_change_them()
    {
        var item = ValidExpense(100m, Share(Fuel, 100m));

        // Same code, now classified Revenue: an edit that leaves the shares alone saves.
        Assert.True(item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Revenue, Item(amount: 100m), ActorId).IsSuccess);
        Assert.True(item.Update(
            CodeId, "FLEET-MAINT", BudgetCodeCategory.Revenue,
            Item(amount: 100m, vendorShares: [Share(Fuel, 100m)]), ActorId).IsSuccess);

        // Changing them is a new decision on a revenue item — refused.
        Assert.Equal(
            BudgetAllocationErrors.VendorSharesNotAllowedForRevenue,
            item.Update(
                CodeId, "FLEET-MAINT", BudgetCodeCategory.Revenue,
                Item(amount: 100m, vendorShares: [Share(Tires, 100m)]), ActorId).Error);

        // Moving the item to another (revenue) code with its shares is a change too.
        Assert.Equal(
            BudgetAllocationErrors.VendorSharesNotAllowedForRevenue,
            item.Update(Guid.NewGuid(), "ZBB-CREW-01", BudgetCodeCategory.Revenue, Item(amount: 100m), ActorId).Error);

        // Clearing them is always allowed.
        Assert.True(item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Revenue, Item(amount: 100m, vendorShares: []), ActorId).IsSuccess);
    }

    [Fact]
    public void An_expense_item_keeps_unchanged_legacy_text_but_cannot_change_it()
    {
        // A $0 legacy item the backfill could not split, or a revenue code re-classified to
        // expense: the item carries text and sits on an expense code.
        var item = CreateAllocation(PeriodId, CodeId, "FLEET-MAINT", details: Item(amount: 0m, vendor: "Kal Tire"));

        Assert.True(item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, Item(amount: 0m, vendor: "Kal Tire", title: "Edited"), ActorId).IsSuccess);
        Assert.Equal(
            BudgetAllocationErrors.PayerNotAllowedForExpense,
            item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, Item(amount: 0m, vendor: "Kal Tire Thompson"), ActorId).Error);
        Assert.True(item.Update(CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense, Item(amount: 0m, vendor: null), ActorId).IsSuccess);
        Assert.Null(item.Vendor);
    }

    [Fact]
    public void A_refused_share_update_changes_nothing_and_raises_nothing()
    {
        var item = ValidExpense(100m, Share(Fuel, 100m));
        item.ClearDomainEvents();

        var result = item.Update(
            CodeId, "FLEET-MAINT", BudgetCodeCategory.Expense,
            Item(amount: 100m, vendorShares: [Share(Fuel, 50m), Share(Fuel, 50m)]), ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(Fuel, Assert.Single(item.VendorShares).VendorId);
        Assert.Empty(item.DomainEvents);
    }

    // --- AddedVendorIds: what the handlers look up ---

    [Fact]
    public void Added_vendor_ids_are_only_the_vendors_the_item_did_not_have()
    {
        var item = ValidExpense(100m, Share(Fuel, 100m));

        Assert.Empty(BudgetAllocation.AddedVendorIds(Item(amount: 100m), item.VendorShares));
        Assert.Empty(BudgetAllocation.AddedVendorIds(Item(amount: 100m, vendorShares: [Share(Fuel, 100m)]), item.VendorShares));
        Assert.Equal(
            [Tires],
            BudgetAllocation.AddedVendorIds(
                Item(amount: 100m, vendorShares: [Share(Fuel, 40m), Share(Tires, 60m), Share(null, 1m)]),
                item.VendorShares));
    }

    // --- Copy ---

    [Fact]
    public void A_copy_onto_an_expense_code_carries_the_shares_as_is()
    {
        var source = ValidExpense(1000.10m, Share(Fuel, 600.05m), Share(Tires, 400.05m));

        var copy = source.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Expense, ActorId);

        Assert.Equal(source.VendorShares, copy.VendorShares);
        Assert.NotSame(source.VendorShares, copy.VendorShares);
        Assert.Equal(source.AmountCad, copy.AmountCad);
    }

    [Fact]
    public void A_copys_shares_are_new_instances_never_the_sources()
    {
        // Equal by value, but EF Core tracks owned JSON entities by REFERENCE: a share instance
        // shared between the tracked source and the added copy is one dependent with two owners,
        // and SaveChanges throws (pinned against real EF in Budgeting.IntegrationTests).
        var source = ValidExpense(1000.10m, Share(Fuel, 600.05m), Share(Tires, 400.05m));

        var copy = source.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Expense, ActorId);

        Assert.Equal(source.VendorShares.Count, copy.VendorShares.Count);
        Assert.All(
            source.VendorShares.Zip(copy.VendorShares),
            pair =>
            {
                Assert.Equal(pair.First, pair.Second);
                Assert.NotSame(pair.First, pair.Second);
            });
    }

    [Fact]
    public void Copy_for_new_owner_is_an_equal_but_distinct_share()
    {
        var share = BudgetItemVendorShare.Create(Fuel, 12.34m).Value;

        var copy = share.CopyForNewOwner();

        Assert.Equal(share, copy);
        Assert.NotSame(share, copy);
    }

    [Fact]
    public void A_copy_follows_the_target_codes_category()
    {
        var expense = ValidExpense(100m, Share(Fuel, 100m));
        var ontoRevenue = expense.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Revenue, ActorId);
        Assert.Empty(ontoRevenue.VendorShares);

        var revenue = CreateAllocation(details: Item(amount: 100m, vendor: "Alamos Gold"));
        Assert.Equal("Alamos Gold", revenue.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Revenue, ActorId).Vendor);

        // Onto an expense code with no shares to carry, the text still crosses rather than being
        // silently lost — the copy is not the planner adding a payer.
        var ontoExpense = revenue.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Expense, ActorId);
        Assert.Equal("Alamos Gold", ontoExpense.Vendor);
        Assert.Empty(ontoExpense.VendorShares);
    }

    [Fact]
    public void A_zero_dollar_legacy_expense_item_keeps_its_vendor_text_through_a_copy_and_stays_saveable()
    {
        // The backfill's leftover: a $0 expense item still holding free text, which it could not
        // turn into a share. Only raw SQL builds that state on an expense code, so the source is a
        // $0 payer item (text allowed) — CopyInto sees the same thing: no shares, some text.
        var legacy = CreateAllocation(details: Item(amount: 0m, vendor: "Gord's Garage"));

        var copy = legacy.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Expense, ActorId);

        Assert.Equal("Gord's Garage", copy.Vendor);
        Assert.True(copy.NeedsJustification);

        // Arguing the copy without touching the text is not a change to the payer, so it saves.
        var argued = copy.Update(
            copy.BudgetCodeId, copy.Code, BudgetCodeCategory.Expense,
            Item(amount: 0m, vendor: "Gord's Garage", justification: "Still needed this year."), ActorId);
        Assert.True(argued.IsSuccess);
        Assert.Equal("Gord's Garage", copy.Vendor);

        // Changing the text is a change, and meets the rule.
        var changed = copy.Update(
            copy.BudgetCodeId, copy.Code, BudgetCodeCategory.Expense,
            Item(amount: 0m, vendor: "Gord's Garage Ltd", justification: "Still needed this year."), ActorId);
        Assert.Equal(BudgetAllocationErrors.PayerNotAllowedForExpense, changed.Error);
    }

    [Fact]
    public void Leftover_text_beside_shares_is_dropped_onto_an_expense_code()
    {
        // Text the item kept (unchanged) while shares were added: the shares supersede it.
        var item = CreateAllocation(details: Item(amount: 0m, vendor: "Old text"));
        Assert.True(item.Update(
            item.BudgetCodeId, item.Code, BudgetCodeCategory.Expense,
            Item(amount: 50m, vendor: "Old text", vendorShares: [Share(Fuel, 50m)]), ActorId).IsSuccess);

        var copy = item.CopyInto(Guid.NewGuid(), Guid.NewGuid(), BudgetCodeCategory.Expense, ActorId);

        Assert.Null(copy.Vendor);
        Assert.Equal(Fuel, Assert.Single(copy.VendorShares).VendorId);
    }
}
