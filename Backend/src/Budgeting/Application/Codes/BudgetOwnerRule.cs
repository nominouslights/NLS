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
    public static async Task<Result> ValidateAsync(
        IUserLookupRepository users,
        Guid? budgetOwnerUserId,
        CancellationToken cancellationToken)
    {
        if (budgetOwnerUserId is not { } userId)
        {
            return Result.Success();
        }

        var owner = await users.GetAsync(userId, cancellationToken);
        return owner is null
            ? Result.Failure(BudgetCodeErrors.BudgetOwnerNotFound)
            : Result.Success();
    }
}
