using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Allocations.GetAllocations;

/// <summary>Lists one period's allocation lines, ordered by code. NotFound when the period does not exist.</summary>
public sealed record GetBudgetAllocationsQuery(Guid TenantId, Guid PeriodId) : IQuery<IReadOnlyList<BudgetAllocationResponse>>;
