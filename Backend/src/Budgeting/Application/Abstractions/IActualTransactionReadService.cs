using NorthernLink.Budgeting.Application.Actuals;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Read side over budgeting.rm_actual_transactions. No tenant parameter — the DbContext's tenant
/// query filter (and RLS underneath it) scopes every query to the ambient tenant.
/// </summary>
public interface IActualTransactionReadService
{
    /// <summary>
    /// Every transaction falling inside an inclusive date range, ordered by date then code, with
    /// the code's current name, category, service line and active flag resolved from
    /// <c>rm_budget_codes</c> at read time.
    /// <para>
    /// A <em>range</em>, not a period id, because a transaction stores no period — its date is
    /// the only thing that ever said which period it belongs to (see <c>ActualTransaction</c>).
    /// The caller resolves the period, which is also where the 404 for an unknown one comes from.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ActualTransactionResponse>> GetForDateRangeAsync(
        DateOnly startsOn, DateOnly endsOn, CancellationToken cancellationToken = default);
}
