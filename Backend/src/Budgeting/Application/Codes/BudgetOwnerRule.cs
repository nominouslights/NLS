using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// Validates a budget code's owner against Budgeting's <c>user_lookup</c> replica of Identity's
/// accounts — shared by create and edit for the same reason as
/// <see cref="BudgetCodeParentRule"/>.
/// <para>
/// This is what makes <c>BudgetOwnerUserId</c> a real reference rather than the free-text name
/// the rest of the codebase uses for "who". The lookup is tenant-filtered, so a user id from
/// another tenant reads back null and reports BudgetOwnerNotFound rather than silently binding.
/// </para>
/// </summary>
public static class BudgetOwnerRule
{
    public static Task<Result> ValidateAsync(
        IUserLookupRepository users,
        Guid? budgetOwnerUserId,
        CancellationToken cancellationToken) =>
        ValidateAsync(users, budgetOwnerUserId, BudgetCodeErrors.BudgetOwnerNotFound, cancellationToken);

    /// <summary>
    /// The same rule for any aggregate that names an accountable user (the cost-centre register
    /// uses it too), reporting <paramref name="notFound"/> — the caller's own error — when the id
    /// names nobody in this tenant's replica.
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IUserLookupRepository users,
        Guid? ownerUserId,
        Error notFound,
        CancellationToken cancellationToken)
    {
        if (ownerUserId is not { } userId)
        {
            return Result.Success();
        }

        var owner = await users.GetAsync(userId, cancellationToken);
        return owner is null
            ? Result.Failure(notFound)
            : Result.Success();
    }
}
