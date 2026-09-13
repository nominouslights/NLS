namespace NorthernLink.Budgeting.Domain.Periods;

/// <summary>
/// The four forward steps of a budget period's life, one per edge of the
/// <see cref="PeriodState"/> order: <see cref="Finalize"/> (Draft → Finalized),
/// <see cref="Open"/> (Finalized → Open), <see cref="BeginReview"/> (Open → InReview) and
/// <see cref="Close"/> (InReview → Closed). One discriminator for one command — the
/// <c>SetBudgetCodeActiveCommand</c> precedent — rather than four commands that would each load,
/// check and save identically.
/// </summary>
public enum PeriodTransition
{
    Finalize,
    Open,
    BeginReview,
    Close,
}
