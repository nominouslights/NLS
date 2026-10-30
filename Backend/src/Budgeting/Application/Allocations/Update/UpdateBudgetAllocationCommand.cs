using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Application.Allocations.Update;

/// <summary>
/// Rewrites one budget item, addressed by (period, item id). The item may move to another budget
/// code (<paramref name="BudgetCodeId"/>); it never moves to another period. Same nullable-code
/// and actor reasoning as <c>CreateBudgetAllocationCommand</c>.
/// </summary>
public sealed record UpdateBudgetAllocationCommand(
    Guid TenantId,
    Guid PeriodId,
    Guid AllocationId,
    Guid? BudgetCodeId,
    BudgetItemDetails Details,
    Guid? ActorId) : ICommand;
