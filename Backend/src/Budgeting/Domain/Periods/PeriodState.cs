namespace NorthernLink.Budgeting.Domain.Periods;

/// <summary>
/// Lifecycle of a budget period, in order: <see cref="Draft"/> → <see cref="Finalized"/> →
/// <see cref="Open"/> → <see cref="InReview"/> → <see cref="Closed"/>. Forward only — there is
/// no path back, and each step is its own <see cref="PeriodTransition"/> so the aggregate can
/// refuse a skipped or repeated step with an error that names the rule.
/// <para>
/// The plan (allocation lines) is editable in <see cref="Draft"/> and <see cref="Open"/> only.
/// Finalizing signs the plan off; opening the period re-allows in-period adjustments; review and
/// close freeze it for good. See <see cref="BudgetPeriod.AllowsPlanChanges"/>.
/// </para>
/// Persisted by name (<c>varchar(16)</c>), so the members must stay short and never be renamed
/// once a row has been written in that state.
/// </summary>
public enum PeriodState
{
    Draft,
    Finalized,
    Open,
    InReview,
    Closed,
}
