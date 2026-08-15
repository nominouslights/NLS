using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.GetOwnerCandidates;

/// <summary>
/// The users who can be named as a budget code's owner — the console's owner picker. Sourced
/// from Budgeting's <c>user_lookup</c> replica of Identity's accounts, so it needs no library
/// reference and works with whatever the replica currently holds.
/// </summary>
public sealed record GetBudgetOwnerCandidatesQuery(Guid TenantId)
    : IQuery<IReadOnlyList<BudgetOwnerOptionResponse>>;

/// <summary>
/// One pickable owner. <paramref name="Email"/> is the display value because Identity's user
/// aggregate has no name field — email is the only human-readable identifier an account has.
/// </summary>
public sealed record BudgetOwnerOptionResponse(Guid UserId, string Email, string Role);
