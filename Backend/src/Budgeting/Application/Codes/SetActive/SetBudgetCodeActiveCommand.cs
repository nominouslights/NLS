using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.SetActive;

/// <summary>
/// Retires (<c>IsActive = false</c>) or restores a budget code — the normal end of a code's
/// life. Deliberately not a delete: allocations and actuals reference codes by string and must
/// keep resolving. <paramref name="ActorId"/> comes from the signed token and stamps
/// <c>modified_by</c>.
/// </summary>
public sealed record SetBudgetCodeActiveCommand(
    Guid TenantId,
    Guid BudgetCodeId,
    bool IsActive,
    Guid? ActorId) : ICommand;
