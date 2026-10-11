using NorthernLink.Budgeting.Application.Allocations;
using NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;
using NorthernLink.Budgeting.Application.Allocations.Create;
using NorthernLink.Budgeting.Application.Allocations.Update;
using NorthernLink.Budgeting.Application.Vendors.Delete;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Vendors;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Budgeting.Tests.TestBudgeting;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// Vendor shares through the handlers: the register checks (vendor must exist, a retired vendor
/// refused only when newly added, a retained share on a since-retired vendor kept), the vendor
/// register lock that closes the delete-vs-add race, the copy carrying shares, and the real usage
/// probe turning a vendor delete into 409 InUse.
/// </summary>
public class BudgetItemVendorShareHandlerTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly InMemoryVendorRepository _vendors = new();

    private readonly CreateBudgetAllocationCommandHandler _create;
    private readonly UpdateBudgetAllocationCommandHandler _update;
    private readonly CopyBudgetAllocationsCommandHandler _copy;
    private readonly DeleteVendorCommandHandler _deleteVendor;

    private readonly BudgetPeriod _draft = CreatePeriod(PeriodGranularity.Quarter, 2026, 4);
    private readonly BudgetCode _expense;
    private readonly BudgetCode _revenue;
    private readonly Vendor _kalTire = CreateVendor("Kal Tire Thompson");
    private readonly Vendor _petro = CreateVendor("Petro-Canada");
    private readonly Vendor _retired = CreateVendor("Old Garage", active: false);

    public BudgetItemVendorShareHandlerTests()
    {
        _create = new CreateBudgetAllocationCommandHandler(_allocations, _periods, _codes, _vendors);
        _update = new UpdateBudgetAllocationCommandHandler(_allocations, _periods, _codes, _vendors);
        _copy = new CopyBudgetAllocationsCommandHandler(_allocations, _periods, _codes, _vendors);
        _deleteVendor = new DeleteVendorCommandHandler(_vendors, new AllocationVendorUsageProbe(_allocations));

        _periods.Add(_draft);
        _expense = AddCode(_draft, "FLEET-MAINT", BudgetCodeCategory.Expense);
        _revenue = AddCode(_draft, "ZBB-CREW-01", BudgetCodeCategory.Revenue);
        _vendors.Vendors.AddRange([_kalTire, _petro, _retired]);
    }

    private BudgetCode AddCode(BudgetPeriod period, string code, BudgetCodeCategory category)
    {
        var budgetCode = CreateCode(
            code,
            CodeDetails(category: category, serviceLine: category == BudgetCodeCategory.Revenue ? BudgetServiceLine.ContractCrew : null),
            periodId: period.Id);
        _codes.Add(budgetCode);
        return budgetCode;
    }

    private Task<Result<Guid>> CreateAsync(BudgetItemDetails details, BudgetCode? code = null) =>
        _create.Handle(
            new CreateBudgetAllocationCommand(TenantId, _draft.Id, (code ?? _expense).Id, details, ActorId),
            CancellationToken.None);

    private Task<Result> UpdateAsync(Guid itemId, BudgetItemDetails details, BudgetCode? code = null) =>
        _update.Handle(
            new UpdateBudgetAllocationCommand(TenantId, _draft.Id, itemId, (code ?? _expense).Id, details, ActorId),
            CancellationToken.None);

    private Task<Result> DeleteVendorAsync(Guid vendorId) =>
        _deleteVendor.Handle(new DeleteVendorCommand(TenantId, vendorId), CancellationToken.None);

    /// <summary>An expense item already holding these shares, as if saved earlier.</summary>
    private BudgetAllocation PlannedExpense(decimal amount, params BudgetItemVendorShareDetails[] shares)
    {
        var item = CreateAllocation(
            _draft.Id, _expense.Id, _expense.Code,
            details: Item(amount: amount, vendorShares: shares),
            category: BudgetCodeCategory.Expense);
        _allocations.Add(item);
        return item;
    }

    // --- Create ---

    [Fact]
    public async Task Create_saves_an_expense_item_split_across_active_vendors_under_the_register_lock()
    {
        var result = await CreateAsync(Item(amount: 900.50m, vendorShares: [Share(_kalTire.Id, 600.25m), Share(_petro.Id, 300.25m)]));

        Assert.True(result.IsSuccess);
        var item = Assert.Single(_allocations.Allocations);
        Assert.Equal([_kalTire.Id, _petro.Id], item.VendorShares.Select(s => s.VendorId).ToArray());

        // Lock first, then the register reads, then the commit (after the item's save).
        Assert.Equal(["Lock", "GetById", "GetById", "Commit"], _vendors.Calls);
        Assert.Equal([TenantId], _vendors.CommittedLocks);
    }

    [Fact]
    public async Task Create_with_no_shares_takes_no_vendor_lock()
    {
        Assert.True((await CreateAsync(Item(amount: 50m))).IsSuccess);

        Assert.Empty(_vendors.Calls);
    }

    [Fact]
    public async Task Create_naming_an_unknown_vendor_is_VendorNotFound_and_saves_nothing()
    {
        var result = await CreateAsync(Item(amount: 10m, vendorShares: [Share(Guid.NewGuid(), 10m)]));

        Assert.Equal(BudgetAllocationErrors.VendorNotFound, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(_allocations.Allocations);
        Assert.Empty(_vendors.CommittedLocks);
    }

    [Fact]
    public async Task Another_tenants_vendor_reads_as_not_found()
    {
        var foreign = CreateVendor("Foreign Fuel", tenantId: Guid.NewGuid());
        _vendors.Vendors.Add(foreign);

        var result = await CreateAsync(Item(amount: 10m, vendorShares: [Share(foreign.Id, 10m)]));

        Assert.Equal(BudgetAllocationErrors.VendorNotFound, result.Error);
    }

    [Fact]
    public async Task Create_naming_a_retired_vendor_is_VendorInactive_naming_it()
    {
        var result = await CreateAsync(Item(amount: 10m, vendorShares: [Share(_retired.Id, 10m)]));

        Assert.Equal("Budgeting.Allocation.VendorInactive", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains("Old Garage", result.Error.Message);
        Assert.Empty(_allocations.Allocations);
    }

    [Fact]
    public async Task Create_with_shares_on_a_revenue_code_is_refused_before_any_register_lookup()
    {
        var result = await CreateAsync(Item(amount: 10m, vendorShares: [Share(Guid.NewGuid(), 10m)]), _revenue);

        // The category is the real mistake, not the unknown vendor — and no lock is taken for it.
        Assert.Equal(BudgetAllocationErrors.VendorSharesNotAllowedForRevenue, result.Error);
        Assert.Empty(_vendors.Calls);
    }

    [Fact]
    public async Task Create_with_a_bad_sum_is_a_validation_error_before_any_lookup()
    {
        var result = await CreateAsync(Item(amount: 10m, vendorShares: [Share(Guid.NewGuid(), 9.99m)]));

        Assert.Equal("Budgeting.Allocation.VendorSharesMustSumToAmount", result.Error.Code);
        Assert.Empty(_vendors.Calls);
    }

    // --- Update ---

    [Fact]
    public async Task A_share_on_a_since_retired_vendor_is_kept_on_update()
    {
        var item = PlannedExpense(100m, Share(_kalTire.Id, 100m));
        _kalTire.SetActive(false, ActorId);

        // Omitted shares: kept.
        Assert.True((await UpdateAsync(item.Id, Item(amount: 100m, title: "Winter tires"))).IsSuccess);
        // Restated shares: still the same vendor, so not "added" — no lookup, no lock.
        Assert.True((await UpdateAsync(item.Id, Item(amount: 100m, vendorShares: [Share(_kalTire.Id, 100m)]))).IsSuccess);

        Assert.Equal(_kalTire.Id, Assert.Single(item.VendorShares).VendorId);
        Assert.Empty(_vendors.Calls);
    }

    [Fact]
    public async Task Adding_a_retired_vendor_on_update_is_refused_even_beside_a_kept_one()
    {
        var item = PlannedExpense(100m, Share(_kalTire.Id, 100m));

        var result = await UpdateAsync(
            item.Id, Item(amount: 100m, vendorShares: [Share(_kalTire.Id, 50m), Share(_retired.Id, 50m)]));

        Assert.Equal("Budgeting.Allocation.VendorInactive", result.Error.Code);
        Assert.Equal(100m, Assert.Single(item.VendorShares).AmountCad);
    }

    [Fact]
    public async Task Adding_an_active_vendor_on_update_looks_up_only_the_new_one_under_the_lock()
    {
        var item = PlannedExpense(100m, Share(_kalTire.Id, 100m));

        var result = await UpdateAsync(
            item.Id, Item(amount: 100m, vendorShares: [Share(_kalTire.Id, 70m), Share(_petro.Id, 30m)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Lock", "GetById", "Commit"], _vendors.Calls);
        Assert.Equal([70m, 30m], item.VendorShares.Select(s => s.AmountCad).ToArray());
    }

    [Fact]
    public async Task Changing_the_amount_with_stale_shares_is_refused_and_changes_nothing()
    {
        var item = PlannedExpense(100m, Share(_kalTire.Id, 60m), Share(_petro.Id, 40m));
        var saves = _allocations.SaveChangesCallCount;

        var result = await UpdateAsync(item.Id, Item(amount: 150m));

        Assert.Equal("Budgeting.Allocation.VendorSharesMustSumToAmount", result.Error.Code);
        Assert.Equal(100m, item.AmountCad);
        Assert.Equal(saves, _allocations.SaveChangesCallCount);
    }

    // --- Copy ---

    [Fact]
    public async Task Copy_carries_shares_as_is_including_a_retired_vendors_and_drops_them_onto_revenue()
    {
        var target = CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(target);
        AddCode(target, "FLEET-MAINT", BudgetCodeCategory.Expense);

        // CHARTER-FUEL is an expense code in the source period but classified Revenue in the target.
        var charterSource = AddCode(_draft, "CHARTER-FUEL", BudgetCodeCategory.Expense);
        AddCode(target, "CHARTER-FUEL", BudgetCodeCategory.Revenue);

        PlannedExpense(1000.10m, Share(_kalTire.Id, 600.05m), Share(_retired.Id, 400.05m));
        _allocations.Add(CreateAllocation(
            _draft.Id, charterSource.Id, charterSource.Code,
            details: Item(amount: 25m, vendorShares: [Share(_petro.Id, 25m)]),
            category: BudgetCodeCategory.Expense));

        var result = await _copy.Handle(
            new CopyBudgetAllocationsCommand(TenantId, target.Id, _draft.Id, ActorId), CancellationToken.None);

        Assert.Equal(2, result.Value.Copied);
        var copies = _allocations.Allocations.Where(a => a.PeriodId == target.Id).ToList();
        var fleet = Assert.Single(copies, a => a.Code == "FLEET-MAINT");
        Assert.Equal(
            [(_kalTire.Id, 600.05m), (_retired.Id, 400.05m)],
            fleet.VendorShares.Select(s => (s.VendorId, s.AmountCad)).ToArray());
        Assert.Empty(Assert.Single(copies, a => a.Code == "CHARTER-FUEL").VendorShares);

        Assert.Equal(["Lock", "Commit"], _vendors.Calls);
    }

    private BudgetPeriod CopyTarget()
    {
        var target = CreatePeriod(PeriodGranularity.Quarter, 2027, 1);
        _periods.Add(target);
        AddCode(target, "FLEET-MAINT", BudgetCodeCategory.Expense);
        return target;
    }

    private Task<Result<BudgetAllocationCopyResult>> CopyAsync(BudgetPeriod target) =>
        _copy.Handle(new CopyBudgetAllocationsCommand(TenantId, target.Id, _draft.Id, ActorId), CancellationToken.None);

    [Fact]
    public async Task Copy_of_a_plan_with_no_shares_takes_no_vendor_lock()
    {
        var target = CopyTarget();
        PlannedExpense(100m);
        PlannedExpense(250m);

        var result = await CopyAsync(target);

        Assert.Equal(2, result.Value.Copied);
        Assert.Empty(_vendors.Calls);
    }

    [Fact]
    public async Task Copy_carries_a_zero_dollar_legacy_items_vendor_text_onto_an_expense_code()
    {
        var target = CopyTarget();
        // The backfill's leftover is a $0 item holding free text and no share, landing on an
        // expense code. The aggregate cannot be built in that state on an expense code (only the
        // backfill's raw SQL makes one), so the same landing is reached through a re-classified
        // code: a $0 payer item on GORD-01, which is Revenue in the source and Expense in the target.
        var gordSource = AddCode(_draft, "GORD-01", BudgetCodeCategory.Revenue);
        AddCode(target, "GORD-01", BudgetCodeCategory.Expense);
        _allocations.Add(CreateAllocation(
            _draft.Id, gordSource.Id, gordSource.Code,
            details: Item(amount: 0m, vendor: "Gord's Garage"),
            category: BudgetCodeCategory.Revenue));

        var result = await CopyAsync(target);

        Assert.Equal(1, result.Value.Copied);
        var copy = Assert.Single(_allocations.Allocations, a => a.PeriodId == target.Id);
        Assert.Equal("Gord's Garage", copy.Vendor);
        Assert.Empty(copy.VendorShares);
        Assert.Empty(_vendors.Calls);
    }

    [Fact]
    public async Task Shares_added_after_the_check_are_locked_late_and_verified_under_the_lock()
    {
        var target = CopyTarget();
        var item = PlannedExpense(100m);

        // Between the untracked "any shares?" (no) and the tracked read, a planner adds one.
        _allocations.AfterVendorSharesCheckAsync = async () =>
            Assert.True((await UpdateAsync(item.Id, Item(amount: 100m, vendorShares: [Share(_kalTire.Id, 100m)]))).IsSuccess);

        var result = await CopyAsync(target);

        Assert.Equal(1, result.Value.Copied);
        var copy = Assert.Single(_allocations.Allocations, a => a.PeriodId == target.Id);
        Assert.Equal(_kalTire.Id, Assert.Single(copy.VendorShares).VendorId);

        // The update's own lock/lookup/commit, then the copy's late lock, its check and its commit.
        Assert.Equal(["Lock", "GetById", "Commit", "Lock", "GetById", "Commit"], _vendors.Calls);
    }

    [Fact]
    public async Task A_carried_vendor_deleted_in_the_gap_fails_the_copy_with_CopySourceChanged_and_writes_nothing()
    {
        var target = CopyTarget();
        var item = PlannedExpense(100m);
        var saves = _allocations.SaveChangesCallCount;

        // The tracked read will see a share on Petro-Canada, but by the time the copy locks, that
        // vendor is gone (its share was removed elsewhere and the vendor deleted — compressed here).
        _allocations.AfterVendorSharesCheckAsync = () =>
        {
            Assert.True(item.Update(
                item.BudgetCodeId, item.Code, BudgetCodeCategory.Expense,
                Item(amount: 100m, vendorShares: [Share(_petro.Id, 100m)]), ActorId).IsSuccess);
            _vendors.Vendors.Remove(_petro);
            return Task.CompletedTask;
        };

        var result = await CopyAsync(target);

        Assert.Equal(BudgetAllocationErrors.CopySourceChanged, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.DoesNotContain(_allocations.Allocations, a => a.PeriodId == target.Id);
        Assert.Equal(saves, _allocations.SaveChangesCallCount);
        Assert.Equal(["Lock", "GetById"], _vendors.Calls);
        Assert.Empty(_vendors.CommittedLocks);
    }

    // --- The real usage probe and the vendor delete ---

    [Fact]
    public async Task A_vendor_with_a_share_in_any_period_is_InUse_and_cannot_be_deleted()
    {
        var closed = TestBudgeting.PeriodIn(PeriodState.Closed, PeriodGranularity.Quarter, 2025, 1);
        _allocations.Add(CreateAllocation(
            closed.Id, Guid.NewGuid(), "FLEET-MAINT",
            details: Item(amount: 10m, vendorShares: [Share(_kalTire.Id, 10m)]),
            category: BudgetCodeCategory.Expense));

        var result = await DeleteVendorAsync(_kalTire.Id);

        Assert.Equal(VendorErrors.InUse, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains(_kalTire, _vendors.Vendors);
        Assert.Empty(_vendors.CommittedLocks);
    }

    [Fact]
    public async Task Another_tenants_share_does_not_make_a_vendor_in_use()
    {
        _allocations.Allocations.Add(CreateAllocation(
            tenantId: Guid.NewGuid(),
            details: Item(amount: 10m, vendorShares: [Share(_petro.Id, 10m)]),
            category: BudgetCodeCategory.Expense));

        Assert.False(await new AllocationVendorUsageProbe(_allocations).IsReferencedAsync(_petro.Id));
        Assert.True((await DeleteVendorAsync(_petro.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_vendor_whose_shares_were_removed_can_be_deleted_under_the_lock()
    {
        var item = PlannedExpense(100m, Share(_petro.Id, 100m));
        Assert.True((await UpdateAsync(item.Id, Item(amount: 100m, vendorShares: []))).IsSuccess);
        _vendors.Calls.Clear();

        var result = await DeleteVendorAsync(_petro.Id);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(_petro, _vendors.Vendors);
        Assert.Equal(["Lock", "GetById", "Commit"], _vendors.Calls.Where(c => c != "Save"));
    }

    [Fact]
    public async Task A_delete_that_wins_the_lock_makes_the_waiting_create_report_VendorNotFound()
    {
        // The create acquires the lock only after a delete of the same vendor committed.
        _vendors.OnLockAcquiredAsync = async () =>
        {
            _vendors.OnLockAcquiredAsync = null;
            Assert.True((await DeleteVendorAsync(_petro.Id)).IsSuccess);
        };

        var result = await CreateAsync(Item(amount: 10m, vendorShares: [Share(_petro.Id, 10m)]));

        Assert.Equal(BudgetAllocationErrors.VendorNotFound, result.Error);
        Assert.Empty(_allocations.Allocations);
    }

    [Fact]
    public async Task A_create_that_wins_the_lock_makes_the_waiting_delete_report_InUse()
    {
        // The delete acquires the lock only after an item naming the vendor committed.
        _vendors.OnLockAcquiredAsync = async () =>
        {
            _vendors.OnLockAcquiredAsync = null;
            Assert.True((await CreateAsync(Item(amount: 10m, vendorShares: [Share(_petro.Id, 10m)]))).IsSuccess);
        };

        var result = await DeleteVendorAsync(_petro.Id);

        Assert.Equal(VendorErrors.InUse, result.Error);
        Assert.Contains(_petro, _vendors.Vendors);
    }
}
