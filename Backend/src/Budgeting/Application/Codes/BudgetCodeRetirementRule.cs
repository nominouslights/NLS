using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// <b>A budget code can only be retired once every period that uses it is Closed.</b> It lives in
/// the application layer rather than on the aggregate for the same reason
/// <see cref="BudgetCodeParentRule"/> does: it needs to see allocation lines, transactions and
/// periods, none of which a <c>BudgetCode</c> can reach.
/// <para>
/// The rule protects the one thing retirement was invented to protect. A retired code stays listed
/// precisely so old rows keep resolving — but retiring a code a <em>live</em> period still plans
/// against pulls it out from under work in progress: the set handler then refuses every edit to
/// those lines with <c>CodeRetired</c>, and a planner is stuck holding a plan they can no longer
/// change. Closed is the point after which nobody needs the code to be live any more.
/// </para>
/// <para>
/// <b>Both kinds of reference count.</b> An allocation line names its period directly; a
/// transaction names only its date, and the period covering that date is derived the same way
/// every other read derives it. A code with no allocation line at all but one transaction in an
/// open period is blocked — that arm is the easy one to miss, and skipping it would leave the
/// actuals of a live period pointing at a code the ledger screen renders as retired.
/// </para>
/// <para>
/// <b>Two consequences worth stating so neither reads as an oversight:</b>
/// </para>
/// <para>
/// A code used by a <c>Draft</c> or <c>Open</c> period can be retired only after its line is
/// removed from that period — which is allowed, because the plan is still editable in both
/// states. So the way out is in the user's hands and the refusal message points at the period.
/// </para>
/// <para>
/// A code used by a <c>Finalized</c> or <c>InReview</c> period cannot be retired until that
/// period closes, because its plan is frozen and the line cannot be removed. That is intended,
/// not a deadlock: a frozen plan is exactly the thing that must keep resolving to a live code.
/// The wait is bounded by the period's own lifecycle.
/// </para>
/// <para>
/// This is deliberately <em>not</em> folded into <see cref="IBudgetCodeUsageProbe"/>. The probe
/// answers "has anything ever referenced this code", which is the right question for the
/// hard-delete refusal and the wrong one here — it returns a bool and carries no period state,
/// and teaching it about lifecycle would make one method mean two different things at its two
/// call sites.
/// </para>
/// </summary>
public static class BudgetCodeRetirementRule
{
    /// <summary>
    /// Success when nothing references the code, or when every period that does is Closed;
    /// otherwise <see cref="BudgetCodeErrors.PeriodNotClosed"/> naming the blocking periods,
    /// earliest first.
    /// <para>
    /// Reads the tenant's periods in full rather than asking the database per reference — the
    /// same working set <c>CreateBudgetPeriodCommandHandler</c> already loads on every period
    /// create, and a tenant has tens of periods, not thousands. Three queries, no joins, and the
    /// date-to-period mapping stays in one readable place.
    /// </para>
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IBudgetAllocationRepository allocations,
        IActualTransactionRepository actuals,
        IBudgetPeriodRepository periods,
        Guid budgetCodeId,
        string code,
        CancellationToken cancellationToken)
    {
        var plannedIn = await allocations.GetPeriodIdsForCodeAsync(budgetCodeId, code, cancellationToken);
        var spentOn = await actuals.GetDatesForCodeAsync(budgetCodeId, code, cancellationToken);

        if (plannedIn.Count == 0 && spentOn.Count == 0)
        {
            return Result.Success();
        }

        var all = await periods.GetAllAsync(cancellationToken);
        var plannedInSet = plannedIn.ToHashSet();

        var blocking = all
            .Where(period => period.State != PeriodState.Closed)
            .Where(period => plannedInSet.Contains(period.Id) || spentOn.Any(period.Covers))
            .OrderBy(period => period.StartsOn)
            .Select(period => period.Label)
            .ToList();

        // A transaction dated outside every period cannot block anything — it is unreachable from
        // the period view by definition, and the record handler refuses to create one. A row like
        // that predates the rule or was written by hand; it must not be able to freeze a code
        // forever, because there is no period anyone could close to release it.
        return blocking.Count == 0
            ? Result.Success()
            : Result.Failure(BudgetCodeErrors.PeriodNotClosed(blocking));
    }
}
