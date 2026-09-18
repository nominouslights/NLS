using NorthernLink.Budgeting.Application.Actuals;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side for one period's plan-against-spend rollup: the allocation lines and the actual
/// transactions of the same period, set beside each other one row per budget code.
/// <para>
/// Its own abstraction rather than a method on either of the two read services, because it
/// belongs to neither — it reads allocations, actuals and the chart together, and hanging it off
/// whichever one happened to come first would make the name lie about what it touches.
/// </para>
/// </summary>
public interface IBudgetRollupReadService
{
    /// <summary>
    /// One row per budget code that has a plan in the period, spend inside the period's dates, or
    /// both — ordered by code. The plan comes from the period id, the spend from the inclusive
    /// date range, because a transaction stores no period id (see <c>ActualTransaction</c>).
    /// </summary>
    Task<IReadOnlyList<BudgetRollupRowResponse>> GetForPeriodAsync(
        Guid periodId, DateOnly startsOn, DateOnly endsOn, CancellationToken cancellationToken = default);
}
