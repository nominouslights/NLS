using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Actuals;

/// <summary>
/// "Some budget period covers this date" — in one place so recording and correcting a transaction
/// cannot drift apart, the same reason <see cref="Codes.BudgetCodeParentRule"/> exists. It lives
/// in the application layer rather than on the aggregate because it needs to see the tenant's
/// periods, which an aggregate by definition cannot.
/// <para>
/// <b>A transaction whose date falls in no period is refused, not stored.</b> The period view is
/// the only place actuals are ever listed, so an orphan row is one nothing in the product can
/// show and nothing in the product can find again — it would sit in the table making the rollup
/// disagree with the sum of its own lines. Creating the period first costs one form. The refusal
/// is a Conflict rather than a validation error because the payload is fine; the world is not
/// ready for it yet.
/// </para>
/// <para>
/// There is no "and only one period" arm, and a reviewer will look for one:
/// <c>CreateBudgetPeriodCommandHandler</c> refuses any period that shares a day with an existing
/// one, so within a tenant a date is covered at most once by construction. Re-deriving that here
/// would be a second, weaker copy of a rule already enforced where periods are made.
/// </para>
/// <para>
/// <b>It returns the covering period, because codes belong to one.</b> Each period owns its chart
/// and the code string is only the cross-period identity, so the handlers look the transaction's
/// code up <em>in this period</em>: a Q3 code on a December transaction is refused as
/// <c>Budgeting.Code.NotFound</c> ("not found in this period"), and moving a transaction's date
/// into another period means re-tagging it with that period's code. Without this, a period's
/// rollup could carry spend against another period's chart.
/// </para>
/// </summary>
public static class CoveringPeriodRule
{
    /// <summary>
    /// The tenant's period that contains <paramref name="occurredOn"/> in its inclusive
    /// StartsOn..EndsOn range; otherwise
    /// <see cref="ActualTransactionErrors.NoPeriodCoversDate"/>.
    /// <para>
    /// Reads every period of the tenant rather than asking the database for the covering one: a
    /// tenant has tens of periods, not thousands, and <c>GetAllAsync</c> is the same working set
    /// the overlap check already loads on every period create.
    /// </para>
    /// </summary>
    public static async Task<Result<BudgetPeriod>> ValidateAsync(
        IBudgetPeriodRepository periods,
        DateOnly occurredOn,
        CancellationToken cancellationToken)
    {
        var all = await periods.GetAllAsync(cancellationToken);

        // BudgetPeriod.Covers, not an inline comparison: the read side maps transaction dates
        // onto periods too, and two copies of "inclusive on both ends" would eventually disagree
        // about a boundary day.
        var covering = all.FirstOrDefault(period => period.Covers(occurredOn));
        return covering is not null
            ? Result.Success(covering)
            : Result.Failure<BudgetPeriod>(ActualTransactionErrors.NoPeriodCoversDate);
    }
}
