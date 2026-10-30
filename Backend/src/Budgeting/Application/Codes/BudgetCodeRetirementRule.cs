using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// <b>A budget code cannot be retired while its own period uses it.</b> It lives in the
/// application layer rather than on the aggregate for the same reason
/// <see cref="BudgetCodeParentRule"/> does: it needs to see budget items, transactions and the
/// period, none of which a <c>BudgetCode</c> can reach.
/// <para>
/// The rule protects the one thing retirement was invented to protect. A retired code stays listed
/// precisely so old rows keep resolving — but retiring a code a live period still plans or spends
/// against pulls it out from under work in progress: the item handlers then refuse every edit to
/// those lines, and a planner is stuck holding a plan they can no longer change.
/// </para>
/// <para>
/// <b>Codes belong to a period, so "its own period" is the only one that counts.</b> An item of
/// another period references <em>that</em> period's code of the same string, and a transaction
/// counts in the period covering its date. Both arms are therefore bounded to the code's period
/// exactly as <see cref="IBudgetCodeUsageProbe"/> bounds them — matching by string across periods
/// would let Q3's FUEL block retiring Q4's.
/// </para>
/// <para>
/// <b>Both kinds of reference count, and the refusal names which one applies</b>, because the
/// remedies differ. Items: remove them first (the plan is editable in Draft and Open), then
/// retire — <see cref="BudgetCodeErrors.RetirementBlockedByItems"/>. Spend dated in the period:
/// the code cannot be retired at all — <see cref="BudgetCodeErrors.RetirementBlockedBySpend"/>.
/// Spend is checked first: when both apply, removing the items would not help, so naming them
/// would send the user down a path that ends in the second refusal. The spend arm is also the
/// easy one to miss — skipping it would leave the period's actuals pointing at a code the ledger
/// screen renders as retired.
/// </para>
/// <para>
/// <b>How this composes with the per-period chart lifecycle.</b>
/// <see cref="SetActive.SetBudgetCodeActiveCommandHandler"/> first requires the code's period to be
/// Draft or Open (<see cref="BudgetCodePeriodRule"/>), so this rule only ever sees a live period.
/// The rule was originally close-gated (retire once every period using the code is Closed); with
/// per-period charts frozen outside Draft/Open that release could never be reached, so it is gone
/// rather than left as dead code. Whether the chart gate should relax for retirement is an open
/// owner decision; if it does, this rule needs its Closed release back.
/// </para>
/// <para>
/// This is deliberately <em>not</em> folded into <see cref="IBudgetCodeUsageProbe"/>. The probe
/// answers "has anything ever referenced this code", which is the right question for the
/// hard-delete refusal; this rule answers per arm with different remedies, and teaching the probe
/// about that would make one method mean two different things at its two call sites.
/// </para>
/// </summary>
public static class BudgetCodeRetirementRule
{
    /// <summary>
    /// Success when nothing in the code's period references it; otherwise
    /// <see cref="BudgetCodeErrors.RetirementBlockedBySpend"/> or
    /// <see cref="BudgetCodeErrors.RetirementBlockedByItems"/> naming the period (spend wins when
    /// both apply). <see cref="BudgetPeriodErrors.NotFound"/> if the period does not exist.
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IBudgetAllocationRepository allocations,
        IActualTransactionRepository actuals,
        IBudgetPeriodRepository periods,
        Guid periodId,
        Guid budgetCodeId,
        string code,
        CancellationToken cancellationToken)
    {
        var period = await periods.GetByIdAsync(periodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure(BudgetPeriodErrors.NotFound);
        }

        if (await actuals.ExistsForCodeAsync(budgetCodeId, code, period.StartsOn, period.EndsOn, cancellationToken))
        {
            return Result.Failure(BudgetCodeErrors.RetirementBlockedBySpend(period.Label));
        }

        return await allocations.ExistsForCodeAsync(period.Id, budgetCodeId, code, cancellationToken)
            ? Result.Failure(BudgetCodeErrors.RetirementBlockedByItems(period.Label))
            : Result.Success();
    }
}
