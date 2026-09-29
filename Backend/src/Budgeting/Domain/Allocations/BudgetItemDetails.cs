namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// Everything a planner types about one budget item, as one parameter object shared by
/// <see cref="BudgetAllocation.Create"/>, <see cref="BudgetAllocation.Update"/> and
/// <see cref="BudgetAllocation.Validate"/> — the <c>BudgetCodeDetails</c> shape, for the same
/// reason: one object makes it structurally impossible for create and edit to drift apart.
/// <para>
/// Raw input, not a normalized value: strings may be padded or blank, the tag list may repeat
/// itself, and <see cref="AmountCad"/> may be present alongside <see cref="Quantity"/> and
/// <see cref="UnitCostCad"/> (it is then ignored — the aggregate computes the amount). Every
/// field is nullable so a missing one fails as a readable domain error rather than a model-binding
/// 400 with no code in it. The budget code, the period and the actor are deliberately absent:
/// they are resolved and checked by the handlers, not chosen alongside the form.
/// </para>
/// </summary>
public sealed record BudgetItemDetails
{
    /// <summary>What the money is for ("Winter tires, unit NL-04"). Required, ≤ 120 after trim.</summary>
    public string? Title { get; init; }

    /// <summary>The lump-sum amount. Required unless quantity and unit cost are both given.</summary>
    public decimal? AmountCad { get; init; }

    /// <summary>How many units. Must come with <see cref="UnitCostCad"/> — both or neither.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>Cost per unit. Must come with <see cref="Quantity"/> — both or neither.</summary>
    public decimal? UnitCostCad { get; init; }

    /// <summary>What one unit is ("month", "litre", "trip"). Kept only when a quantity is given.</summary>
    public string? Unit { get; init; }

    /// <summary>Why this money, argued from zero. Required.</summary>
    public string? Justification { get; init; }

    public BudgetSpendType SpendType { get; init; } = BudgetSpendType.Operating;

    public BudgetRecurrence Recurrence { get; init; } = BudgetRecurrence.OneTime;

    /// <summary>Free-text payee or supplier.</summary>
    public string? Vendor { get; init; }

    /// <summary>Free-form labels. Trimmed, de-duplicated case-insensitively, at most 10.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    public BudgetItemPriority Priority { get; init; } = BudgetItemPriority.ShouldHave;

    /// <summary>The cost-driver assumptions behind the number.</summary>
    public string? Assumptions { get; init; }

    /// <summary>What happens if this item is cut.</summary>
    public string? ConsequenceIfUnfunded { get; init; }
}
