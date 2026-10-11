using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// One vendor's dollar share of an <b>expense</b> budget item: this vendor
/// (<see cref="VendorId"/>, an entry of the tenant's vendor register) is planned to be paid this
/// much (<see cref="AmountCad"/>) of the item. An item's shares, when it has any, sum to exactly
/// its <see cref="BudgetAllocation.AmountCad"/> — the rule lives on the aggregate, because a single
/// share cannot see its siblings.
/// <para>
/// Persisted as jsonb inside the owning item (column <c>vendor_shares</c>), so like the Fleet
/// work-order defect line it has no id of its own: within one item it is keyed by
/// <see cref="VendorId"/> (each vendor at most once). The vendor is a bare id — no navigation, no
/// foreign key, the aggregate-boundary rule every Budgeting reference follows; its name is
/// resolved from <c>rm_vendors</c> at read time, never snapshotted, so a renamed vendor reads
/// right everywhere.
/// </para>
/// <para>
/// <b>Dollars, not percentages.</b> A percentage split re-derives its dollars from the amount
/// every time the amount moves; the owner's rule is the opposite — the planner states the
/// dollars, and changing the amount without restating them is refused, never silently
/// rebalanced. No tax field: the platform never computes tax.
/// </para>
/// </summary>
public sealed record BudgetItemVendorShare
{
    private BudgetItemVendorShare()
    {
        // EF Core JSON materialization only.
    }

    /// <summary>The vendor-register entry this share pays. Never <see cref="Guid.Empty"/>.</summary>
    public Guid VendorId { get; private init; }

    /// <summary>The share in Canadian dollars: positive, at most two decimal places, at most <see cref="BudgetAllocation.AmountMax"/>.</summary>
    public decimal AmountCad { get; private init; }

    /// <summary>
    /// Validates one raw share. The amount is <b>not rounded</b>: a share of 10.005 is refused
    /// rather than quietly stored as 10.01, because the shares must sum to the item's amount to the
    /// cent, and a silent round could make a sum the planner checked by hand come out a cent off.
    /// </summary>
    public static Result<BudgetItemVendorShare> Create(Guid? vendorId, decimal? amountCad)
    {
        if (vendorId is not { } id || id == Guid.Empty)
        {
            return Result.Failure<BudgetItemVendorShare>(BudgetAllocationErrors.VendorShareVendorRequired);
        }

        if (amountCad is not { } amount
            || amount <= 0m
            || amount > BudgetAllocation.AmountMax
            || decimal.Round(amount, 2) != amount)
        {
            return Result.Failure<BudgetItemVendorShare>(BudgetAllocationErrors.VendorShareAmountInvalid);
        }

        return Result.Success(new BudgetItemVendorShare { VendorId = id, AmountCad = amount });
    }
}

/// <summary>
/// One vendor share as a planner sends it — raw input, every field nullable so a missing one fails
/// as a readable domain error (<c>VendorShareVendorRequired</c>, <c>VendorShareAmountInvalid</c>)
/// rather than a model-binding 400 with no code in it.
/// </summary>
public sealed record BudgetItemVendorShareDetails(Guid? VendorId, decimal? AmountCad);
