using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Periods.Transition;

/// <summary>
/// Moves a budget period one step forward along Draft → Finalized → Open → InReview → Closed.
/// One command with a <see cref="PeriodTransition"/> discriminator, four routes — the
/// <c>SetBudgetCodeActiveCommand</c> precedent. Which step is legal is the aggregate's call;
/// the handler only loads, delegates and saves. <paramref name="ActorId"/> comes from the signed
/// token and rides the state-changed event.
/// </summary>
public sealed record TransitionBudgetPeriodCommand(
    Guid TenantId,
    Guid PeriodId,
    PeriodTransition Transition,
    Guid? ActorId) : ICommand;
