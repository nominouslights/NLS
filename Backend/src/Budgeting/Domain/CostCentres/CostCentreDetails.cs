namespace NorthernLink.Budgeting.Domain.CostCentres;

/// <summary>
/// Everything about a cost centre a planner can change after it exists, as one parameter object
/// shared by <see cref="CostCentre.Create"/> and <see cref="CostCentre.Update"/> — the
/// <c>BudgetCodeDetails</c> shape, for the same reason: create and edit cannot drift apart.
/// <para>
/// <b><see cref="CostCentre.Code"/> is deliberately absent.</b> Budget codes carry the cost
/// centre by its code string (that string is what crosses periods), so rewriting it would orphan
/// every code already tagged. It is supplied once, at creation.
/// </para>
/// </summary>
public sealed record CostCentreDetails
{
    /// <summary>What the unit is called ("Thompson base").</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The accountable person, validated against Budgeting's <c>user_lookup</c> replica in the
    /// application layer (the aggregate cannot see the replica).
    /// </summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>
    /// Optional parent for a one-level rollup. Validated in the application layer
    /// (<c>CostCentreParentRule</c>): the parent must exist in the tenant's register, be
    /// top-level, and be active when it is chosen.
    /// </summary>
    public Guid? ParentId { get; init; }
}
