namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// What <c>SetBudgetAllocationCommand</c> returns. The set is an upsert, so the caller cannot know
/// from the request alone whether it added a line or rewrote one; <paramref name="Created"/>
/// says which, and <paramref name="AllocationId"/> is stable across both — the same id on the
/// update that the create handed out.
/// </summary>
public sealed record BudgetAllocationSetResult(Guid AllocationId, bool Created);
