using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;

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
/// </summary>
public static class CoveringPeriodRule
{
    /// <summary>
    /// Success when one of the tenant's periods contains <paramref name="occurredOn"/> in its
    /// inclusive StartsOn..EndsOn range; otherwise
    /// <see cref="ActualTransactionErrors.NoPeriodCoversDate"/>.
    /// <para>
    /// Reads every period of the tenant rather than asking the database for the covering one: a
    /// tenant has tens of periods, not thousands, and <c>GetAllAsync</c> is the same working set
    /// the overlap check already loads on every period create.
    /// </para>
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IBudgetPeriodRepository periods,
        DateOnly occurredOn,
        CancellationToken cancellationToken)
    {
        var all = await periods.GetAllAsync(cancellationToken);

        return all.Any(period => period.StartsOn <= occurredOn && occurredOn <= period.EndsOn)
            ? Result.Success()
            : Result.Failure(ActualTransactionErrors.NoPeriodCoversDate);
    }
}
