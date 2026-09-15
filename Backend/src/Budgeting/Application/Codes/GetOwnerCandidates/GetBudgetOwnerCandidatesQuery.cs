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
/// One pickable owner. Both identifiers travel rather than one pre-resolved display string: a
/// picker is exactly where two people with similar names have to be told apart, and
/// <paramref name="Email"/> is the unique one. <paramref name="FullName"/> is null for anyone who
/// has not set a profile, so the client's label falls back to the email.
/// </summary>
public sealed record BudgetOwnerOptionResponse(
    Guid UserId, string Email, string Role, string? FullName);
