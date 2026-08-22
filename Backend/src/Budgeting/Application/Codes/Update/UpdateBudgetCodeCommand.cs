using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Update;

/// <summary>
/// Rewrites a budget code's descriptive details. There is no Code on this command by design —
/// see <see cref="BudgetCodeDetails"/>: the code string is decided once and never renamed.
/// <paramref name="ActorId"/> comes from the signed token and stamps <c>modified_by</c>.
/// </summary>
public sealed record UpdateBudgetCodeCommand(
    Guid TenantId,
    Guid BudgetCodeId,
    BudgetCodeDetails Details,
    Guid? ActorId) : ICommand;
