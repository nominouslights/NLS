using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// <b>A budget code can only be retired once every period that uses it is Closed.</b> It lives in
/// the application layer rather than on the aggregate for the same reason
/// <see cref="BudgetCodeParentRule"/> does: it needs to see budget items, transactions and the
/// period, none of which a <c>BudgetCode</c> can reach.
/// <para>
/// The rule protects the one thing retirement was invented to protect. A retired code stays listed
/// precisely so old rows keep resolving — but retiring a code a <em>live</em> period still plans
/// or spends against pulls it out from under work in progress: the item handlers then refuse every
/// edit to those lines, and a planner is stuck holding a plan they can no longer change. Closed is
/// the point after which nobody needs the code to be live any more.
/// </para>
/// <para>
/// <b>Codes belong to a period, so "every period that uses it" is the code's own period.</b> An
/// item of another period references <em>that</em> period's code of the same string, and a
/// transaction counts in the period covering its date. Both arms are therefore bounded to the
/// code's period exactly as <see cref="IBudgetCodeUsageProbe"/> bounds them — matching by string
/// across periods would let Q3's FUEL block retiring Q4's.
/// </para>
/// <para>
/// <b>Both kinds of reference count.</b> A code with no item at all but one transaction dated in
/// its period is blocked — that arm is the easy one to miss, and skipping it would leave the
/// period's actuals pointing at a code the ledger screen renders as retired.
/// </para>
/// <para>
/// <b>How this composes with the per-period chart lifecycle.</b>
/// <see cref="SetActive.SetBudgetCodeActiveCommandHandler"/> first requires the code's period to be
/// Draft or Open (<see cref="BudgetCodePeriodRule"/>), and neither is Closed — so in practice a
/// code its own period references cannot be retired at all; only one nothing in its period uses
/// can. A planned code can be released by removing its items first (the plan is editable in both
/// states); a code with spend recorded in its period has no such way out. That is the literal
/// intersection of the two rules as they were written — tenant-wide codes with a close-gated
/// retirement, and per-period charts frozen outside Draft/Open — and it is left strict on purpose
/// until the owner decides which of the two should give.
/// </para>
/// <para>
/// This is deliberately <em>not</em> folded into <see cref="IBudgetCodeUsageProbe"/>. The probe
/// answers "has anything ever referenced this code", which is the right question for the
/// hard-delete refusal; this rule adds the period state, and teaching the probe about lifecycle
/// would make one method mean two different things at its two call sites.
/// </para>
/// </summary>
public static class BudgetCodeRetirementRule
{
    /// <summary>
    /// Success when nothing in the code's period references it, or when that period is Closed;
    /// otherwise <see cref="BudgetCodeErrors.PeriodNotClosed"/> naming the period.
    /// <see cref="BudgetPeriodErrors.NotFound"/> if the period does not exist.
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

        if (period.State == PeriodState.Closed)
        {
            return Result.Success();
        }

        var used =
            await allocations.ExistsForCodeAsync(period.Id, budgetCodeId, code, cancellationToken)
            || await actuals.ExistsForCodeAsync(budgetCodeId, code, period.StartsOn, period.EndsOn, cancellationToken);

        return used
            ? Result.Failure(BudgetCodeErrors.PeriodNotClosed([period.Label]))
            : Result.Success();
    }
}
